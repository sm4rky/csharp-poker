using System.Collections.Concurrent;
using PokerAppBackend.Domain;
using ShowdownResult = PokerAppBackend.Domain.ShowdownResult;

namespace PokerAppBackend.Services;

public sealed class TableService : ITableService
{
    private readonly ConcurrentDictionary<string, Table> _tables = new();
    private readonly IEvaluateHandService _evaluateHandService;
    private static readonly TimeSpan DeleteTableTimer = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cleanupTimers = new();

    public TableService(IEvaluateHandService evaluateHandService)
    {
        this._evaluateHandService = evaluateHandService;
    }

    public string CreateTable(int playerCount)
    {
        if (playerCount is < 2 or > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));

        var code = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var players = Enumerable.Repeat<string?>(null, playerCount).ToArray();
        var table = new Table(code, playerCount, players);
        if (!_tables.TryAdd(code, table))
            throw new InvalidOperationException("Failed to create table.");
        
        ScheduleTableCleanup(code, DateTime.UtcNow);
        return code;
    }

    public Table Get(string tableCode)
    {
        return _tables.TryGetValue(tableCode, out var table)
            ? table
            : throw new KeyNotFoundException("Table not found. Start a new table first.");
    }

    public void JoinAsPlayer(string tableCode, int seatIndex, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("creatorName is required.", nameof(name));

        var table = Get(tableCode);

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        if (table.IsHandInProgress())
            throw new InvalidOperationException("Cannot join in the middle of a hand. Please wait for next hand.");

        table.JoinAsPlayer(seatIndex, name);
        CancelTableScheduledCleanup(tableCode);
    }

    public void SetSeatToBot(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);
        var wasAllBots = table.AllBotsSinceUtc is not null;
        table.SetSeatToBot(seatIndex);
        if (!wasAllBots && table.AllBotsSinceUtc is not null)
        {
            ScheduleTableCleanup(tableCode, table.AllBotsSinceUtc.Value);
        }
    }

    public void StartHand(string tableCode)
    {
        Get(tableCode).StartHand();
    }

    public void DealFlop(string tableCode)
    {
        Get(tableCode).DealFlop();
    }

    public void DealTurn(string tableCode)
    {
        Get(tableCode).DealTurn();
    }

    public void DealRiver(string tableCode)
    {
        Get(tableCode).DealRiver();
    }

    public void Check(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);

        if (table.Street is Street.Showdown)
            throw new InvalidOperationException("Hand already ended.");

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        table.Check(seatIndex);
    }

    public void Call(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);

        if (table.Street is Street.Showdown)
            throw new InvalidOperationException("Hand already ended.");

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        table.Call(seatIndex);
    }

    public void Raise(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);

        if (table.Street is Street.Showdown)
            throw new InvalidOperationException("Hand already ended.");

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        table.Raise(seatIndex);
    }

    public FoldResult Fold(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);

        if (table.Street is Street.Showdown)
            throw new InvalidOperationException("Cannot fold in this state.");

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        return table.Fold(seatIndex);
    }

    public ShowdownResult Showdown(string tableCode)
    {
        var table = Get(tableCode);
        if (table.Street is not Street.River and not Street.Showdown)
            throw new InvalidOperationException("Cannot showdown before river.");

        var contenders = table.Players
            .Where(player => !player.HasFolded)
            .ToList();

        if (contenders.Count == 0)
            return new ShowdownResult { Winners = Array.Empty<int>(), Scored = new() };

        var scored = new List<(Player Player, HandValue HandValue)>(contenders.Count);
        foreach (var p in contenders)
        {
            var hv = _evaluateHandService.EvaluateHand(p.Hole, table.Community);
            scored.Add((p, hv));
        }

        var bestHand = scored.Max(tuple => tuple.HandValue);

        var winners = scored
            .Where(tuple => tuple.HandValue.CompareTo(bestHand) == 0)
            .Select(tuple => tuple.Player.SeatIndex)
            .OrderBy(seatIndex => seatIndex)
            .ToArray();

        return new ShowdownResult
        {
            Winners = winners,
            Scored = scored
        };
    }
    
    private void ScheduleTableCleanup(string tableCode, DateTime allBotsSinceUtc)
    {
        CancelTableScheduledCleanup(tableCode);

        var cts = new CancellationTokenSource();
        if (!_cleanupTimers.TryAdd(tableCode, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = DeleteTableTimer - (DateTime.UtcNow - allBotsSinceUtc);
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

                await Task.Delay(delay, cts.Token);

                if (!_tables.TryGetValue(tableCode, out var table))
                    return;

                if (table.AllBotsSinceUtc is not null &&
                    table.AllBotsSinceUtc.Value == allBotsSinceUtc &&
                    DateTime.UtcNow - table.AllBotsSinceUtc.Value >= DeleteTableTimer)
                {
                    _tables.TryRemove(tableCode, out _);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            finally
            {
                CancelTableScheduledCleanup(tableCode, disposeOnly: true);
            }
        });
    }

    private void CancelTableScheduledCleanup(string tableCode, bool disposeOnly = false)
    {
        if (!_cleanupTimers.TryRemove(tableCode, out var cts)) return;
        if (!disposeOnly)
            cts.Cancel();
        cts.Dispose();
    }
}