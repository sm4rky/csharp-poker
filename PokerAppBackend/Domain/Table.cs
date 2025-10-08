namespace PokerAppBackend.Domain;

public sealed class Table
{
    public string TableCode { get; }
    public Street Street { get; private set; } = Street.Preflop;
    public List<Player> Players { get; }
    public List<Card> Community { get; } = [];
    private Deck _deck = Deck.Standard();
    public int Dealer { get; private set; } = 0;
    public int SmallBlind => NextSeat(Dealer);
    public int BigBlind => NextSeat(SmallBlind);

    public int? CurrentSeatToAct { get; private set; }
    public int? ClosingSeat { get; private set; }

    private int? _lastRaiseSeat = null;

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
    }

    public void StartHand()
    {
        Dealer = NextSeat(Dealer);

        Community.Clear();
        foreach (var player in Players) player.ClearHand();

        _deck = Deck.Standard();
        _deck.Shuffle();

        for (var i = 0; i < 2; i++)
            foreach (var player in Players)
                player.Receive(_deck.Draw());

        Street = Street.Preflop;
        BeginActionRoundForStreet();
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
        BeginActionRoundForStreet();
    }

    public void DealTurn()
    {
        EnsureStreet(Street.Flop);
        _deck.Burn();
        Community.Add(_deck.Draw());
        Street = Street.Turn;
        BeginActionRoundForStreet();
    }

    public void DealRiver()
    {
        EnsureStreet(Street.Turn);
        _deck.Burn();
        Community.Add(_deck.Draw());
        Street = Street.River;
        BeginActionRoundForStreet();
    }

    public bool IsHandInProgress() =>
        Street is Street.Flop or Street.Turn or Street.River;

    public void Check(int seatIndex)
    {
        EnsureActionTurn(seatIndex);
        if (_lastRaiseSeat is not null)
            throw new InvalidOperationException("Cannot check when a raise is pending. You must call or fold.");

        if (ClosingSeat == seatIndex)
        {
            AdvanceToNextStreet();
            return;
        }

        CurrentSeatToAct = NextActiveSeat(seatIndex);
    }

    public void Call(int seatIndex)
    {
        EnsureActionTurn(seatIndex);
        if (_lastRaiseSeat is null)
            throw new InvalidOperationException("Nothing to call. You should check instead.");

        if (ClosingSeat == seatIndex)
        {
            AdvanceToNextStreet();
            return;
        }

        CurrentSeatToAct = NextActiveSeat(seatIndex);
    }

    public void Raise(int seatIndex)
    {
        EnsureActionTurn(seatIndex);

        _lastRaiseSeat = seatIndex;
        ClosingSeat = PrevActiveSeat(seatIndex);
        CurrentSeatToAct = NextActiveSeat(seatIndex);

        foreach (var p in Players)
        {
            if (p.HasFolded || Street == Street.Showdown)
                p.SetLegalActions([]);
            else
                p.SetLegalActions([PlayerAction.Fold, PlayerAction.Call, PlayerAction.Raise]);
        }
    }

    public FoldResult Fold(int seatIndex)
    {
        EnsureActionTurn(seatIndex);

        var player = Players[seatIndex];
        if (player.HasFolded) return new FoldResult(false, -1);

        player.Fold();
        player.SetLegalActions([]);

        if (ActivePlayerCount() <= 1)
        {
            Street = Street.Showdown;
            CurrentSeatToAct = null;
            ClosingSeat = null;
            _lastRaiseSeat = null;
            foreach (var p in Players) p.SetLegalActions([]);
            var winner = Players.First(p => !p.HasFolded).SeatIndex;
            return new FoldResult(true, winner);
        }

        if (seatIndex == ClosingSeat)
        {
            AdvanceToNextStreet();
            return new FoldResult(false, -1);
        }

        CurrentSeatToAct = NextActiveSeat(seatIndex);
        return new FoldResult(false, -1);
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

    private void BeginActionRoundForStreet()
    {
        _lastRaiseSeat = null;

        foreach (var p in Players)
        {
            if (p.HasFolded || Street == Street.Showdown)
                p.SetLegalActions([]);
            else
                p.SetLegalActions([PlayerAction.Fold, PlayerAction.Check, PlayerAction.Raise]);
        }

        int firstSeatToAct;
        int closingSeat;

        if (Street is Street.Preflop)
        {
            if (Players.Count == 2)
            {
                firstSeatToAct = SmallBlind;
                closingSeat = BigBlind;
            }
            else
            {
                firstSeatToAct = NextSeat(BigBlind);
                closingSeat = Dealer;
            }
        }
        else
        {
            if (Players.Count == 2)
            {
                firstSeatToAct = BigBlind;
                closingSeat = SmallBlind;
            }
            else
            {
                firstSeatToAct = SmallBlind;
                closingSeat = Dealer;
            }
        }

        firstSeatToAct = IsSeatActive(firstSeatToAct) ? firstSeatToAct : NextActiveSeat(firstSeatToAct);
        closingSeat = IsSeatActive(closingSeat) ? closingSeat : NextActiveSeat(closingSeat);

        CurrentSeatToAct = firstSeatToAct;
        ClosingSeat = closingSeat;
    }

    private void AdvanceToNextStreet()
    {
        CurrentSeatToAct = null;
        ClosingSeat = null;
        _lastRaiseSeat = null;

        switch (Street)
        {
            case Street.Preflop:
                DealFlop();
                return;
            case Street.Flop:
                DealTurn();
                return;
            case Street.Turn:
                DealRiver();
                return;
            case Street.River:
                Street = Street.Showdown;
                foreach (var p in Players) p.SetLegalActions([]);
                return;
            case Street.Showdown:
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private int ActivePlayerCount() => Players.Count(p => !p.HasFolded);

    private bool IsSeatActive(int seat) => !Players[seat].HasFolded;

    private int NextSeat(int currentSeat) => (currentSeat + 1) % Players.Count;
    private int PrevSeat(int currentSeat) => (currentSeat - 1 + Players.Count) % Players.Count;

    private int NextActiveSeat(int currentSeat)
    {
        var n = Players.Count;
        for (var i = 1; i < n; i++)
        {
            var seat = (currentSeat + i) % n;
            if (IsSeatActive(seat)) return seat;
        }

        return currentSeat;
    }

    private int PrevActiveSeat(int currentSeat)
    {
        var n = Players.Count;
        for (var i = 1; i < n; i++)
        {
            var seat = (currentSeat - i + n) % n;
            if (IsSeatActive(seat)) return seat;
        }

        return currentSeat;
    }
}