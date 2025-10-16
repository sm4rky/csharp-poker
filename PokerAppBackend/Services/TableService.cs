using System.Collections.Concurrent;
using PokerAppBackend.Domain;
using ShowdownResult = PokerAppBackend.Domain.ShowdownResult;

namespace PokerAppBackend.Services;

public sealed class TableService(IEvaluateHandService evaluateHandService) : ITableService
{
    private readonly ConcurrentDictionary<string, Table> _tables = new();
    private static readonly TimeSpan DeleteTableTimer = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cleanupTimers = new();

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

    public void Raise(string tableCode, int seatIndex, int amount)
    {
        var table = Get(tableCode);

        if (table.Street is Street.Showdown)
            throw new InvalidOperationException("Hand already ended.");

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        table.Raise(seatIndex, amount);
    }

    public FoldResult Fold(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);

        if (table.Street is Street.Showdown)
            throw new InvalidOperationException("Cannot fold in this state.");

        if (seatIndex < 0 || seatIndex >= table.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        var result = table.Fold(seatIndex);

        if (result.IsMatchOver)
        {
            var potTotal = table.Pot;
            table.Players[result.Winner].WinChips(potTotal);
            foreach (var player in table.Players) player.ResetCommitmentForNewHand();
        }

        return result;
    }


    public ShowdownResult Showdown(string tableCode)
    {
        var table = Get(tableCode);
        if (table.Street is not Street.River and not Street.Showdown)
            throw new InvalidOperationException("Cannot showdown before river.");

        var contenders = table.Players
            .Where(player => player is { HasFolded: false, Hole.Count: 2, CommittedThisHand: > 0 })
            .ToList();

        if (contenders.Count == 0)
            return new ShowdownResult { Winners = [], Scored = [] };

        var handValueMap = new Dictionary<Player, HandValue>(table.Players.Count);
        foreach (var p in table.Players)
        {
            var hv = p is { HasFolded: false, Hole.Count: 2, CommittedThisHand: > 0 }
                ? evaluateHandService.EvaluateHand(p.Hole, table.Community)
                : new HandValue(HandRank.Unknown, 0, 0, 0, 0, 0);

            handValueMap[p] = hv;
        }

        var sidePots = table.BuildSidePotsSnapshot();
        foreach (var pot in sidePots)
        {
            var eligibleContenders = pot.EligiblePlayers.Where(p => !p.HasFolded).ToList();
            if (eligibleContenders.Count == 0) continue;
            if (eligibleContenders.Count == 1)
            {
                eligibleContenders[0].WinChips(pot.Total);
                continue;
            }

            var potScored = eligibleContenders
                .Select(c => (Player: c, HandValue: handValueMap[c]))
                .ToList();

            var bestPotHand = potScored.Max(tuple => tuple.HandValue);

            var potWinners = potScored
                .Where(x => x.HandValue.CompareTo(bestPotHand) == 0)
                .Select(x => x.Player)
                .ToList();

            var baseShare = pot.Total / potWinners.Count;
            var remainder = pot.Total % potWinners.Count;

            foreach (var winner in potWinners) winner.WinChips(baseShare);
            if (remainder <= 0) continue;
            var nearestDealerMap = new Dictionary<int, Player>(potWinners.Count);
            var diffBetweenDealerAndWinner = table.Players.Count - 1;
            foreach (var w in potWinners)
            {
                var diff = ((w.SeatIndex - (table.Dealer + 1) + table.Players.Count) % table.Players.Count);
                diffBetweenDealerAndWinner = Math.Min(diffBetweenDealerAndWinner, diff);
                nearestDealerMap[diff] = w;
            }

            var playerToGetRemainder = nearestDealerMap[diffBetweenDealerAndWinner];
            playerToGetRemainder.WinChips(remainder);
        }
        foreach (var player in table.Players) player.ResetCommitmentForNewHand();

        var scored = table.Players
            .Select(p => (Player: p, HandValue: handValueMap[p]))
            .ToList();

        var bestHand = scored
            .Where(x => !x.Player.HasFolded)
            .Max(x => x.HandValue);

        var winners = scored
            .Where(x => !x.Player.HasFolded && x.HandValue.CompareTo(bestHand) == 0)
            .Select(x => x.Player.SeatIndex)
            .OrderBy(i => i)
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