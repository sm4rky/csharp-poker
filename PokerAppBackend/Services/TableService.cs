using System.Collections.Concurrent;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public sealed class TableService(IEvaluateHandService evaluateHandService, IStreetAdvisorService streetAdvisorService)
    : ITableService
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
        var t = Get(tableCode);
        t.StartHand();
        BeginActionRoundForStreet(t);
    }

    public void DealFlop(string tableCode)
    {
        var t = Get(tableCode);
        t.DealFlop();
        BeginActionRoundForStreet(t);
    }

    public void DealTurn(string tableCode)
    {
        var t = Get(tableCode);
        t.DealTurn();
        BeginActionRoundForStreet(t);
    }

    public void DealRiver(string tableCode)
    {
        var t = Get(tableCode);
        t.DealRiver();
        BeginActionRoundForStreet(t);
    }

    public void Check(string tableCode, int seatIndex)
    {
        var t = Get(tableCode);
        GuardAction(t, seatIndex);

        t.Check(seatIndex);
        AdvanceToNextSeat(tableCode, seatIndex);
    }

    public void Call(string tableCode, int seatIndex)
    {
        var t = Get(tableCode);
        GuardAction(t, seatIndex);

        t.Call(seatIndex);
        AdvanceToNextSeat(tableCode, seatIndex);
    }

    public void Raise(string tableCode, int seatIndex, int amount)
    {
        var t = Get(tableCode);
        GuardAction(t, seatIndex);

        t.Raise(seatIndex, amount);
        AdvanceToNextSeat(tableCode, seatIndex);
    }

    public FoldResult Fold(string tableCode, int seatIndex)
    {
        var t = Get(tableCode);
        if (t.Street is Street.Showdown)
            throw new InvalidOperationException("Cannot fold in this state.");
        if (seatIndex < 0 || seatIndex >= t.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");

        var result = t.Fold(seatIndex);

        if (result.IsMatchOver)
        {
            var potTotal = t.Pot;
            t.Players[result.Winner].WinChips(potTotal);
            foreach (var player in t.Players) player.ResetCommitmentForNewHand();
            return result;
        }

        AdvanceToNextSeat(tableCode, seatIndex);
        return result;
    }

    public ShowdownResult Showdown(string tableCode)
    {
        var table = Get(tableCode);
        if (table.Street is not Street.River and not Street.Showdown)
            throw new InvalidOperationException("Cannot showdown before river.");

        var eligible = table.Players
            .Where(p => p is { HasFolded: false, IsOut: false, Hole.Count: 2, CommittedThisHand: > 0 })
            .ToList();

        if (eligible.Count == 0)
            return new ShowdownResult { Winners = [], Scored = [] };

        var handValueMap = new Dictionary<Player, HandValue>(table.Players.Count);
        foreach (var p in table.Players)
        {
            var hv = eligible.Contains(p)
                ? evaluateHandService.EvaluateHand(p.Hole, table.Community)
                : new HandValue(HandRank.Unknown, 0, 0, 0, 0, 0);

            handValueMap[p] = hv;
        }

        var sidePots = table.BuildSidePotsSnapshot();
        foreach (var pot in sidePots)
        {
            var potEligible = pot.EligiblePlayers
                .Where(p => eligible.Contains(p))
                .ToList();

            switch (potEligible.Count)
            {
                case 0:
                    continue;
                case 1:
                    potEligible[0].WinChips(pot.Total);
                    continue;
            }

            var scoredPot = potEligible.Select(p => (Player: p, HV: handValueMap[p])).ToList();
            var bestPot = scoredPot.Max(x => x.HV);

            var potWinners = scoredPot
                .Where(x => x.HV.CompareTo(bestPot) == 0)
                .Select(x => x.Player)
                .ToList();

            var baseShare = pot.Total / potWinners.Count;
            var remainder = pot.Total % potWinners.Count;

            foreach (var w in potWinners) w.WinChips(baseShare);

            if (remainder <= 0) continue;
            {
                var nearest = potWinners
                    .Select(w => (w,
                        diff: (w.SeatIndex - (table.Dealer + 1) + table.Players.Count) % table.Players.Count))
                    .OrderBy(t => t.diff)
                    .First().w;
                nearest.WinChips(remainder);
            }
        }

        var scoredAll = table.Players.Select(p => (Player: p, HandValue: handValueMap[p])).ToList();
        var bestOverall = eligible.Max(p => handValueMap[p]);
        var winnersSeats = eligible
            .Where(p => handValueMap[p].CompareTo(bestOverall) == 0)
            .Select(p => p.SeatIndex)
            .OrderBy(i => i)
            .ToArray();

        foreach (var player in table.Players) player.ResetCommitmentForNewHand();

        return new ShowdownResult
        {
            Winners = winnersSeats,
            Scored = scoredAll
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

    private void BeginActionRoundForStreet(Table table)
    {
        if (table.OnlyOneOrLessPlayerCanActThisStreet())
        {
            AdvanceToNextStreet(table);
            return;
        }

        var boardAdvisory = streetAdvisorService.BuildBoardAdvisory(table);
        table.SetBoardAdvisory(boardAdvisory);
        foreach (var player in table.Players)
        {
            if (table.Community.Count < 3 && player.Hole.Count < 2) continue;
            var playerAdvisory = streetAdvisorService.BuildPlayerAdvisory(table, player.SeatIndex);
            player.SetPlayerAdvisory(playerAdvisory);
        }

        table.ComputeAllPlayersLegalActions();

        int firstSeatToAct;
        int closingSeat;

        if (table.Street is Street.PreFlop)
        {
            if (table.Players.Count(p => table.CanSeatAct(p.SeatIndex)) == 2)
            {
                firstSeatToAct = table.SmallBlind;
                closingSeat = table.BigBlind;
            }
            else
            {
                firstSeatToAct = table.NextSeat(table.BigBlind);
                closingSeat = table.BigBlind;
            }
        }
        else
        {
            if (table.Players.Count(p => table.CanSeatAct(p.SeatIndex)) == 2)
            {
                firstSeatToAct = table.BigBlind;
                closingSeat = table.SmallBlind;
            }
            else
            {
                firstSeatToAct = table.SmallBlind;
                closingSeat = table.Dealer;
            }
        }

        firstSeatToAct = table.CanSeatAct(firstSeatToAct) ? firstSeatToAct : table.NextActingSeat(firstSeatToAct);
        closingSeat = table.CanSeatAct(closingSeat) ? closingSeat : table.NextActingSeat(closingSeat);

        table.SetPreviousSeatToAct(null);
        table.SetCurrentSeatToAct(firstSeatToAct);
        table.SetClosingSeat(closingSeat);
    }

    private void AdvanceToNextStreet(Table table)
    {
        table.ResetAtStreetStart();
        table.IncreaseActionSeq();

        switch (table.Street)
        {
            case Street.PreFlop:
                table.DealFlop();
                break;
            case Street.Flop:
                table.DealTurn();
                break;
            case Street.Turn:
                table.DealRiver();
                break;
            case Street.River:
                table.SetStreet(Street.Showdown);
                foreach (var p in table.Players) p.SetLegalActions([]);
                return;
            case Street.Showdown:
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }

        BeginActionRoundForStreet(table);
    }

    private void AdvanceToNextSeat(string tableCode, int justActedSeat)
    {
        var table = Get(tableCode);

        table.SetPreviousSeatToAct(table.CurrentSeatToAct);

        if (table.ClosingSeat == justActedSeat)
        {
            AdvanceToNextStreet(table);
            return;
        }

        var next = table.NextActingSeat(justActedSeat);

        if (next == justActedSeat)
        {
            AdvanceToNextStreet(table);
            return;
        }

        var hops = 0;
        while (table.Players[next].Stack == 0 && hops < table.Players.Count)
        {
            table.Players[next].SetLatestAction(PlayerAction.AllIn);
            if (next == table.ClosingSeat)
            {
                AdvanceToNextStreet(table);
                return;
            }

            next = table.NextActingSeat(next);
            hops++;
            if (next != justActedSeat) continue;
            AdvanceToNextStreet(table);
            return;
        }

        if (next == justActedSeat)
        {
            AdvanceToNextStreet(table);
            return;
        }

        foreach (var player in table.Players)
        {
            if (table.Community.Count < 3 || player.Hole.Count < 2) continue;
            var playerAdvisory = streetAdvisorService.BuildPlayerAdvisory(table, player.SeatIndex);
            player.SetPlayerAdvisory(playerAdvisory);
        }

        table.SetCurrentSeatToAct(next);
        table.ComputeAllPlayersLegalActions();
        table.IncreaseActionSeq();
    }

    private static void GuardAction(Table t, int seatIndex)
    {
        if (t.Street is Street.Showdown)
            throw new InvalidOperationException("Hand already ended.");
        if (seatIndex < 0 || seatIndex >= t.Players.Count)
            throw new ArgumentOutOfRangeException(nameof(seatIndex), "Seat out of range.");
    }
}