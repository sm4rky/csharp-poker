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
    public PlayerAction LatestAction { get; private set; } = PlayerAction.None;
    public bool IsOut { get; private set; } = false;
    public int Stack { get; private set; } = 5000;
    public int CommittedThisStreet { get; private set; } = 0;
    public int CommittedThisHand { get; private set; } = 0;

    internal void Receive(Card c) => _hole.Add(c);

    public void Fold()
    {
        _hole.Clear();
        HasFolded = true;
        _legalActions.Clear();
        LatestAction = PlayerAction.Fold;
    }

    internal void ClearHand()
    {
        _hole.Clear();
        HasFolded = false;
        _legalActions.Clear();
        LatestAction = PlayerAction.None;
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

    internal int CommitChips(int amount)
    {
        var commit = Math.Min(amount, Stack);
        Stack -= commit;
        CommittedThisStreet += commit;
        CommittedThisHand += commit;
        return commit;
    }

    internal void WinChips(int amount) => Stack += amount;

    internal void ResetCommitmentForNewStreet() => CommittedThisStreet = 0;

    internal void ResetCommitmentForNewHand()
    {
        CommittedThisStreet = 0;
        CommittedThisHand = 0;
    }
    
    internal void SetLatestAction(PlayerAction action) => LatestAction = action;
    
    internal void SetOut() => IsOut = true;
}