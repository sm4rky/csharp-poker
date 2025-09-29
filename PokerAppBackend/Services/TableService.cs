using System.Collections.Concurrent;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public sealed class TableService : ITableService
{
    private readonly ConcurrentDictionary<string, Table> _tables = new();

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
}