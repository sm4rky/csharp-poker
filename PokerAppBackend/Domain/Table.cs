namespace PokerAppBackend.Domain;

public sealed class Table
{
    public string TableCode { get; }
    public Street Street { get; private set; } = Street.Preflop;
    public List<Player> Players { get; }
    public List<Card> Community { get; } = [];
    private Deck _deck = Deck.Standard();

    public Table(string tableCode, int playerCount, IEnumerable<string?> initialNamesOrNullForBot)
    {
        if (playerCount is < 2 or > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));

        TableCode = tableCode;

        Players = Enumerable.Range(0, playerCount)
            .Select(i =>
            {
                var name = initialNamesOrNullForBot.ElementAtOrDefault(i);
                return new Player(i, name ?? $"Bot {i + 1}", isBot: name is null);
            })
            .ToList();
    }

    public void StartHand()
    {
        Community.Clear();
        foreach (var player in Players) player.ClearHand();

        _deck = Deck.Standard();
        _deck.Shuffle();

        for (var i = 0; i < 2; i++)
            foreach (var player in Players)
                player.Receive(_deck.Draw());

        Street = Street.Preflop;
    }

    private void EnsureStreet(Street expected)
    {
        if (Street != expected)
            throw new InvalidOperationException($"Invalid street transition. Expected {expected}, current {Street}.");
    }

    public void DealFlop()
    {
        EnsureStreet(Street.Preflop);
        _deck.Burn();
        Community.AddRange(_deck.DrawMany(3));
        Street = Street.Flop;
    }

    public void DealTurn()
    {
        EnsureStreet(Street.Flop);
        _deck.Burn();
        Community.Add(_deck.Draw());
        Street = Street.Turn;
    }

    public void DealRiver()
    {
        EnsureStreet(Street.Turn);
        _deck.Burn();
        Community.Add(_deck.Draw());
        Street = Street.River;
    }
    
    public bool IsHandInProgress() =>
        Street is Street.Flop or Street.Turn or Street.River;
}