namespace PokerAppBackend.Domain;

public sealed class Table
{
    public string TableCode { get; }
    public int Round { get; private set; } = 0;

    public Street Street { get; private set; } = Street.Waiting;
    public List<Player> Players { get; }
    public List<Card> Community { get; } = [];
    private Deck _deck = Deck.Standard();
    public int Dealer { get; private set; } = 0;
    public int SmallBlind => NextActingSeat(Dealer);
    public int BigBlind => NextActingSeat(SmallBlind);
    public int? CurrentSeatToAct { get; private set; }
    public int? PreviousSeatToAct { get; private set; }
    public int? ClosingSeat { get; private set; }
    private int? _lastRaiseSeat = null;
    public DateTime? AllBotsSinceUtc { get; private set; }

    private readonly BlindLevel[] _blindLevels =
    [
        new BlindLevel(50, 100),
        new BlindLevel(75, 150),
        new BlindLevel(100, 200),
        new BlindLevel(150, 300),
        new BlindLevel(300, 600),
        new BlindLevel(600, 1200),
        new BlindLevel(1000, 2000)
    ];

    private int _blindLevelIndex = -1;

    public BlindLevel CurrentBlindLevel => _blindLevelIndex >= 0
        ? _blindLevels[Math.Min(_blindLevelIndex, _blindLevels.Length - 1)]
        : _blindLevels[0];

    public int Pot => Players.Sum(p => p.CommittedThisHand);
    public int CurrentBet { get; private set; } = 0;
    public int LastRaiseSize { get; private set; } = 0;
    private readonly PotManager _potManager = new PotManager();
    public BoardAdvisory? BoardAdvisory { get; private set; }

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

        Dealer = Random.Shared.Next(0, playerCount);
        AllBotsSinceUtc = DateTime.UtcNow;
    }

    public void JoinAsPlayer(int seatIndex, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("creatorName is required.", nameof(name));

        if (seatIndex < 0 || seatIndex >= Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        var player = Players[seatIndex];

        if (!player.IsBot)
            throw new InvalidOperationException("You can only replace a bot.");

        player.SetHuman(name);
        RefreshAllBotsFlag();
    }

    public void SetSeatToBot(int seatIndex)
    {
        var player = Players[seatIndex];
        player.SetBot($"Bot {seatIndex + 1}");
        RefreshAllBotsFlag();
    }

    public void StartHand()
    {
        Dealer = NextActingSeat(Dealer);

        Round += 1;

        if (Round % Players.Count == 1)
        {
            _blindLevelIndex = Math.Min(_blindLevelIndex + 1, _blindLevels.Length - 1);
        }

        Community.Clear();
        _potManager.ResetAll();
        BoardAdvisory = null;
        foreach (var player in Players)
        {
            player.ClearHand();
            player.ResetCommitmentForNewHand();
            player.SetPlayerAdvisory(null);
            if (player.Stack <= 0) player.SetOut();
        }

        _deck = Deck.Standard();
        _deck.Shuffle();

        for (var i = 0; i < 2; i++)
            foreach (var player in Players.Where(player => player is { IsOut: false, Stack: > 0 }))
                player.Receive(_deck.Draw());

        Street = Street.PreFlop;

        var sbSeat = NextActingSeat(Dealer);
        var bbSeat = NextActingSeat(sbSeat);

        var sb = Players[sbSeat].CommitChips(CurrentBlindLevel.SmallBlindAmount);
        var bb = Players[bbSeat].CommitChips(CurrentBlindLevel.BigBlindAmount);
        _potManager.Add(sbSeat, sb);
        _potManager.Add(bbSeat, bb);

        CurrentBet = CurrentBlindLevel.BigBlindAmount;
        LastRaiseSize = bb;
        _lastRaiseSeat = bbSeat;
    }

    public void DealFlop()
    {
        EnsureStreet(Street.PreFlop);
        _deck.Burn();
        Community.AddRange(_deck.DrawMany(3));
        Street = Street.Flop;
        _lastRaiseSeat = null;
        LastRaiseSize = CurrentBlindLevel.BigBlindAmount;
        CurrentBet = 0;
        foreach (var p in Players) p.ResetCommitmentForNewStreet();
    }

    public void DealTurn()
    {
        EnsureStreet(Street.Flop);
        _deck.Burn();
        Community.Add(_deck.Draw());
        Street = Street.Turn;
        _lastRaiseSeat = null;
        LastRaiseSize = CurrentBlindLevel.BigBlindAmount;
        CurrentBet = 0;
        foreach (var p in Players) p.ResetCommitmentForNewStreet();
    }

    public void DealRiver()
    {
        EnsureStreet(Street.Turn);
        _deck.Burn();
        Community.Add(_deck.Draw());
        Street = Street.River;
        _lastRaiseSeat = null;
        LastRaiseSize = CurrentBlindLevel.BigBlindAmount;
        CurrentBet = 0;
        foreach (var p in Players) p.ResetCommitmentForNewStreet();
    }

    private void EnsureStreet(Street expected)
    {
        if (Street != expected)
            throw new InvalidOperationException($"Invalid street transition. Expected {expected}, current {Street}.");
    }

    public bool IsHandInProgress() =>
        Street is Street.Flop or Street.Turn or Street.River;

    public void Check(int seatIndex)
    {
        EnsureActionTurn(seatIndex);
        if (CurrentBet - Players[seatIndex].CommittedThisStreet > 0)
            throw new InvalidOperationException("Cannot check when a raise is pending. You must call or fold.");

        Players[seatIndex].SetLatestAction(PlayerAction.Check);
    }

    public void Call(int seatIndex)
    {
        EnsureActionTurn(seatIndex);
        if (_lastRaiseSeat is null || CurrentBet == 0)
            throw new InvalidOperationException("Nothing to call. You should check instead.");

        var player = Players[seatIndex];
        var neededAmountToCall = Math.Max(0, CurrentBet - player.CommittedThisStreet);
        if (neededAmountToCall <= 0)
            throw new InvalidOperationException("Nothing to call. You should check instead.");

        Bet(seatIndex, CurrentBet);
        player.SetLatestAction(player.Stack == 0 ? PlayerAction.AllIn : PlayerAction.Call);
    }

    public void Raise(int seatIndex, int amount)
    {
        EnsureActionTurn(seatIndex);
        var player = Players[seatIndex];

        if (amount <= player.CommittedThisStreet)
            throw new InvalidOperationException("Raise amount must exceed your current commitment.");

        if (CurrentBet > 0 && amount <= CurrentBet)
            throw new InvalidOperationException("Raise must exceed the current bet. Use Call instead.");

        var maxRaiseTo = player.CommittedThisStreet + player.Stack;
        if (amount > maxRaiseTo)
            throw new InvalidOperationException("Insufficient stack for this raise amount.");

        int minRaiseTo;
        bool isUndersized;
        var isAllIn = (amount == maxRaiseTo);

        if (CurrentBet == 0)
        {
            if (amount < CurrentBlindLevel.BigBlindAmount)
                throw new InvalidOperationException("First bet on this street must be at least the big blind.");
            minRaiseTo = amount;
            isUndersized = false;
        }
        else
        {
            minRaiseTo = CurrentBet + LastRaiseSize;
            isUndersized = amount < minRaiseTo;
            if (isUndersized && !isAllIn)
                throw new InvalidOperationException("Undersized raise is not allowed unless it is an all-in.");
        }

        Bet(seatIndex, amount);
        var newCurrentBet = player.CommittedThisStreet;

        if (CurrentBet == 0)
        {
            CurrentBet = newCurrentBet;
            LastRaiseSize = Math.Max(LastRaiseSize, newCurrentBet);
        }
        else
        {
            var different = newCurrentBet - CurrentBet;
            CurrentBet = newCurrentBet;
            if (!isUndersized)
                LastRaiseSize = Math.Max(LastRaiseSize, different);
        }

        _lastRaiseSeat = seatIndex;
        player.SetLatestAction(isAllIn ? PlayerAction.AllIn : PlayerAction.Raise);
        ClosingSeat = PrevActingSeat(seatIndex);
    }

    public FoldResult Fold(int seatIndex)
    {
        EnsureActionTurn(seatIndex);

        var player = Players[seatIndex];
        if (player.HasFolded) return new FoldResult(false, -1);

        player.Fold();

        if (ActivePlayerCountEligible() > 1) return new FoldResult(false, -1);
        Street = Street.Showdown;
        CurrentSeatToAct = null;
        ClosingSeat = null;
        _lastRaiseSeat = null;
        foreach (var p in Players) p.SetLegalActions([]);
        var winner = Players
            .First(p => p is { HasFolded: false, IsOut: false, Hole.Count: 2 })
            .SeatIndex;
        return new FoldResult(true, winner);
    }

    private void EnsureActionTurn(int seatIndex)
    {
        if (CurrentSeatToAct is null)
            throw new InvalidOperationException("No action round in progress.");
        if (Players[seatIndex].HasFolded)
            throw new InvalidOperationException("Player already folded.");
        if (seatIndex != CurrentSeatToAct)
            throw new InvalidOperationException("Not your turn.");
    }

    private void Bet(int seatIndex, int amount)
    {
        var player = Players[seatIndex];
        if (amount <= player.CommittedThisStreet) return;
        var need = amount - player.CommittedThisStreet;
        var committedChips = Players[seatIndex].CommitChips(need);
        _potManager.Add(seatIndex, committedChips);
    }

    public IReadOnlyList<SidePot> BuildSidePotsSnapshot()
    {
        return _potManager.BuildSidePots(Players)
            .Select(x => new SidePot(
                x.total,
                x.participatingPlayers
                    .OrderBy(p => p.SeatIndex)
                    .ToArray()))
            .ToList();
    }

    private int ActivePlayerCountEligible() =>
        Players.Count(p => p is { HasFolded: false, IsOut: false, Hole.Count: 2 });

    internal bool CanSeatAct(int seatIndex) => !Players[seatIndex].HasFolded && Players[seatIndex].Stack > 0;

    internal bool OnlyOneOrLessPlayerCanActThisStreet()
    {
        var count = 0;
        foreach (var p in Players.Where(p => !p.HasFolded && p.Stack > 0))
        {
            count++;
            if (count > 1)
                return false;
        }

        return true;
    }


    internal int NextSeat(int currentSeat) => (currentSeat + 1) % Players.Count;
    private int PrevSeat(int currentSeat) => (currentSeat - 1 + Players.Count) % Players.Count;

    internal int NextActingSeat(int currentSeat)
    {
        var n = Players.Count;
        for (var i = 1; i < n; i++)
        {
            var seat = (currentSeat + i) % n;
            if (CanSeatAct(seat)) return seat;
        }

        return currentSeat;
    }

    private int PrevActingSeat(int currentSeat)
    {
        var n = Players.Count;
        for (var i = 1; i < n; i++)
        {
            var seat = (currentSeat - i + n) % n;
            if (CanSeatAct(seat)) return seat;
        }

        return currentSeat;
    }

    private PlayerAction[] ComputeLegalActions(int seatIndex)
    {
        var player = Players[seatIndex];

        if (Street == Street.Showdown || player.HasFolded) return [];
        if (player.Stack == 0) return [];

        var neededAmountToCall = Math.Max(0, CurrentBet - player.CommittedThisStreet);
        var canRaise = false;
        var actions = new List<PlayerAction> { PlayerAction.Fold };

        if (neededAmountToCall == 0)
        {
            actions.Add(PlayerAction.Check);
        }
        else
        {
            if (player.Stack > 0)
            {
                actions.Add(PlayerAction.Call);
            }
        }

        if (player.Stack > neededAmountToCall)
        {
            var minRaiseTo = (CurrentBet == 0)
                ? CurrentBlindLevel.BigBlindAmount
                : CurrentBet + LastRaiseSize;

            var maxRaiseTo = player.CommittedThisStreet + player.Stack;
            if (maxRaiseTo >= minRaiseTo) canRaise = true;
        }

        if (canRaise) actions.Add(PlayerAction.Raise);
        if (player.Stack > 0) actions.Add(PlayerAction.AllIn);

        return actions.ToArray();
    }

    internal void ComputeAllPlayersLegalActions()
    {
        for (var seatIndex = 0; seatIndex < Players.Count; seatIndex++)
            Players[seatIndex].SetLegalActions(ComputeLegalActions(seatIndex));
    }

    public Player? GetLastStanding()
    {
        Player? only = null;
        foreach (var p in Players.Where(p => p.Stack > 0))
        {
            if (only is not null) return null;
            only = p;
        }

        return only;
    }

    internal void SetStreet(Street street) => Street = street;

    internal void SetPreviousSeatToAct(int? seatIndex) => PreviousSeatToAct = seatIndex;

    internal void SetCurrentSeatToAct(int? seatIndex) => CurrentSeatToAct = seatIndex;

    internal void SetClosingSeat(int? seatIndex) => ClosingSeat = seatIndex;

    internal void ResetAtStreetStart()
    {
        CurrentSeatToAct = null;
        ClosingSeat = null;
        _lastRaiseSeat = null;
        foreach (var p in Players) p.SetLatestAction(PlayerAction.None);
    }

    internal void SetBoardAdvisory(BoardAdvisory? boardAdvisory) => BoardAdvisory = boardAdvisory;

    private void RefreshAllBotsFlag()
    {
        if (Players.All(p => p.IsBot))
        {
            AllBotsSinceUtc ??= DateTime.UtcNow;
        }
        else
        {
            AllBotsSinceUtc = null;
        }
    }
}