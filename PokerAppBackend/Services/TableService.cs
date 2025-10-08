using System.Collections.Concurrent;
using PokerAppBackend.Domain;
using ShowdownResult = PokerAppBackend.Domain.ShowdownResult;

namespace PokerAppBackend.Services;

public sealed class TableService : ITableService
{
    private readonly ConcurrentDictionary<string, Table> _tables = new();
    private readonly IEvaluateHandService evaluateHandService;

    public TableService(IEvaluateHandService evaluateHandService)
    {
        this.evaluateHandService = evaluateHandService;
    }

    public string CreateTable(int playerCount, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("creatorName is required.", nameof(name));

        if (playerCount is < 2 or > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));

        var code = Guid.NewGuid().ToString("N")[..6].ToUpper();
        var players = Enumerable.Repeat<string?>(null, playerCount).ToArray();
        players[0] = name;
        var table = new Table(code, playerCount, players);

        return _tables.TryAdd(code, table)
            ? code
            : throw new InvalidOperationException("Failed to create table.");
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

        var player = table.Players[seatIndex];

        if (!player.IsBot)
            throw new InvalidOperationException("You can only replace a bot.");

        player.SetHuman(name);
    }

    public void SetSeatToBot(string tableCode, int seatIndex)
    {
        var table = Get(tableCode);
        var player = table.Players[seatIndex];
        player.SetBot($"Bot {seatIndex + 1}");
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
        if (table.Street is not Street.River)
            throw new InvalidOperationException("Cannot showdown before river.");

        var contenders = table.Players
            .Where(player => !player.HasFolded)
            .ToList();

        if (contenders.Count == 0)
            return new ShowdownResult { Winners = Array.Empty<int>(), Scored = new() };

        var scored = new List<(Player Player, HandValue HandValue)>(contenders.Count);
        foreach (var p in contenders)
        {
            var hv = evaluateHandService.EvaluateHand(p.Hole, table.Community);
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
}