namespace PokerAppBackend.Domain;

public sealed class Player(int seatIndex, string name, bool isBot)
{
    public int SeatIndex { get; } = seatIndex;
    public string Name { get; private set; } = name;
    public bool IsBot { get; private set; } = isBot;
    public bool HasFolded { get; private set; }
    private readonly List<Card> _hole = new(2);
    public IReadOnlyList<Card> Hole => _hole;
    private readonly List<PlayerAction> _legalActions = new();
    public IReadOnlyList<PlayerAction> LegalActions => _legalActions;

    internal void Receive(Card c) => _hole.Add(c);

    public void Fold() {
        _hole.Clear();
        HasFolded = true;
        _legalActions.Clear();
    }

    internal void ClearHand()
    {
        _hole.Clear();
        HasFolded = false;
        _legalActions.Clear();
    }

    internal void SetLegalActions(IEnumerable<PlayerAction> actions)
    {
        _legalActions.Clear();
        _legalActions.AddRange(actions);
    }

    public void SetHuman(string name)
    {
        Name = name;
        IsBot = false;
    }

    public void SetBot(string name)
    {
        Name = name;
        IsBot = true;
    }
}