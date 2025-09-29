namespace PokerAppBackend.Domain;

public sealed class Player(int seatIndex, string name, bool isBot)
{
    public int SeatIndex { get; } = seatIndex;
    public string Name { get; private set; } = name;
    public bool IsBot { get; private set; } = isBot;
    public bool HasFolded { get; private set; }
    private readonly List<Card> _hole = new(2);
    public IReadOnlyList<Card> Hole => _hole;
    
    internal void Receive(Card c) => _hole.Add(c);
    
    public void Fold() => HasFolded = true;
    
    internal void ClearHand()
    {
        _hole.Clear();
        HasFolded = false;
    }
    
    public void SetHuman(string name) { Name = name; IsBot = false; }
}