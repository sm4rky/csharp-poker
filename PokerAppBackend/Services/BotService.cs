using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public sealed class BotService : IBotService
{
    private const double PostflopCallMargin = 0.06;
    private const double PostflopRaiseStrongEquity = 0.70;
    private const double PostflopProbeEquity = 0.45;
    private const double PostflopWetBoost = 0.05;

    private const double PreflopRaiseScore = 85;
    private const double PreflopCallScore = 70;

    public BotAction SetBotAction(Table table, int seatIndex, PlayerAdvisory? playerAdvisory,
        BoardAdvisory? boardAdvisory, (bool canRaise, int minTo, int maxTo) raiseBounds)
    {
        var player = table.Players[seatIndex];
        var legalActions = player.LegalActions;

        if (legalActions.Count == 0) return new BotAction(PlayerAction.Check);

        return playerAdvisory is not null
            ? SetPostflopAction(table, seatIndex, playerAdvisory, boardAdvisory, raiseBounds, legalActions)
            : SetPreflopAction(table, seatIndex, raiseBounds, legalActions);
    }

    private static BotAction SetPostflopAction(Table table, int seatIndex, PlayerAdvisory playerAdvisory,
        BoardAdvisory? boardAdvisory, (bool canRaise, int minTo, int maxTo) raiseBounds,
        IReadOnlyList<PlayerAction> legalActions)
    {
        var potMath = playerAdvisory.PotMath;
        var toCall = potMath.NeededAmountToCall;
        var breakEvenEquity = potMath.BreakEvenEquity;
        var equity = playerAdvisory.StrengthPercentile / 100.0;

        var extra = (boardAdvisory?.Texture == BoardTexture.Wet) ? PostflopWetBoost : 0.0;

        if (toCall == 0)
        {
            if (equity >= PostflopRaiseStrongEquity - extra && raiseBounds.canRaise &&
                legalActions.Contains(PlayerAction.Raise))
            {
                var raiseTo = PickValueRaiseTo(table, raiseBounds, boardAdvisory);
                return new BotAction(PlayerAction.Raise, raiseTo);
            }

            if (equity >= PostflopProbeEquity + extra && raiseBounds.canRaise &&
                legalActions.Contains(PlayerAction.Raise))
            {
                var raiseTo = PickSmallProbeTo(table, seatIndex, raiseBounds);
                return new BotAction(PlayerAction.Raise, raiseTo);
            }

            if (legalActions.Contains(PlayerAction.Check)) return new BotAction(PlayerAction.Check);
            if (legalActions.Contains(PlayerAction.Call)) return new BotAction(PlayerAction.Call);
            return new BotAction(PlayerAction.Fold);
        }

        if (equity >= breakEvenEquity + PostflopCallMargin + extra)
        {
            if (IsStrongMadeHand(playerAdvisory) && raiseBounds.canRaise && legalActions.Contains(PlayerAction.Raise))
            {
                var raiseTo = PickValueRaiseTo(table, raiseBounds, boardAdvisory);
                return new BotAction(PlayerAction.Raise, raiseTo);
            }

            if (IsStrongDraw(playerAdvisory) && raiseBounds.canRaise && legalActions.Contains(PlayerAction.Raise))
            {
                var raiseTo = PickSemiBluffRaiseTo(table, raiseBounds);
                return new BotAction(PlayerAction.Raise, raiseTo);
            }

            if (legalActions.Contains(PlayerAction.Call)) return new BotAction(PlayerAction.Call);
        }
        else
        {
            if (!IsCallOkayWithDraw(playerAdvisory, breakEvenEquity, extra))
            {
                if (legalActions.Contains(PlayerAction.Fold)) return new BotAction(PlayerAction.Fold);
            }
            else
            {
                if (legalActions.Contains(PlayerAction.Call)) return new BotAction(PlayerAction.Call);
            }
        }

        if (legalActions.Contains(PlayerAction.Call)) return new BotAction(PlayerAction.Call);
        if (legalActions.Contains(PlayerAction.Fold)) return new BotAction(PlayerAction.Fold);
        if (legalActions.Contains(PlayerAction.Check)) return new BotAction(PlayerAction.Check);
        return new BotAction(PlayerAction.Fold);
    }

    private static bool IsStrongMadeHand(PlayerAdvisory pa)
    {
        return pa.CurrentHandRank >= HandRank.TwoPair;
    }

    private static bool IsStrongDraw(PlayerAdvisory pa)
    {
        return pa.FlushDraw == FlushDrawType.FourFlush || pa.StraightDraw is StraightDrawType.Oesd;
    }

    private static bool IsCallOkayWithDraw(PlayerAdvisory pa, double be, double extra)
    {
        if (pa.FlushDraw == FlushDrawType.FourFlush || pa.StraightDraw == StraightDrawType.Oesd)
            return true;

        if (pa.Overcards >= 1 && pa.PotMath.BreakEvenEquity < 0.33 + extra)
            return true;

        return false;
    }

    private static int PickValueRaiseTo(Table table, (bool canRaise, int minTo, int maxTo) raiseBounds,
        BoardAdvisory? boardAdvisory)
    {
        var pot = table.Pot;
        var target = (boardAdvisory?.Texture == BoardTexture.Wet) ? pot + (int)(0.90 * pot) : pot + (int)(0.65 * pot);

        var candidate = Math.Max(raiseBounds.minTo, target);
        return Clamp(candidate, raiseBounds.minTo, raiseBounds.maxTo, 10);
    }

    private static int PickSemiBluffRaiseTo(Table t, (bool canRaise, int minTo, int maxTo) bounds)
    {
        var pot = t.Pot;
        var target = pot + (int)(0.75 * pot);
        var candidate = Math.Max(bounds.minTo, target);
        return Clamp(candidate, bounds.minTo, bounds.maxTo, 10);
    }

    private static int PickSmallProbeTo(Table t, int seatIndex, (bool canRaise, int minTo, int maxTo) bounds)
    {
        var pot = t.Pot;
        var target = pot / 2;
        var candidate = Math.Max(bounds.minTo, target);
        return Clamp(candidate, bounds.minTo, bounds.maxTo, 10);
    }

    private static BotAction SetPreflopAction(Table table, int seatIndex,
        (bool canRaise, int minTo, int maxTo) raiseBounds, IReadOnlyList<PlayerAction> legal)
    {
        var player = table.Players[seatIndex];
        var toCall = Math.Max(0, table.CurrentBet - player.CommittedThisStreet);
        var potMath = BuildPotMathFor(table, seatIndex);

        var (hi, lo, suited, gap) = InspectHole(player.Hole);
        var score = ScorePreflop(hi, lo, suited, gap);

        if (toCall == 0)
        {
            if (score >= PreflopRaiseScore && raiseBounds.canRaise && legal.Contains(PlayerAction.Raise))
            {
                var raiseTo = Math.Max(raiseBounds.minTo, table.CurrentBlindLevel.BigBlindAmount * 3);
                return new BotAction(PlayerAction.Raise, Clamp(raiseTo, raiseBounds.minTo, raiseBounds.maxTo, 10));
            }

            if (legal.Contains(PlayerAction.Check)) return new BotAction(PlayerAction.Check);
            if (legal.Contains(PlayerAction.Call)) return new BotAction(PlayerAction.Call);
            return new BotAction(PlayerAction.Fold);
        }

        if (score >= PreflopRaiseScore && raiseBounds.canRaise && legal.Contains(PlayerAction.Raise))
        {
            var target = table.CurrentBet + Math.Max(table.LastRaiseSize, table.CurrentBlindLevel.BigBlindAmount * 2);
            var raiseTo = Clamp(target, raiseBounds.minTo, raiseBounds.maxTo, 10);
            return new BotAction(PlayerAction.Raise, raiseTo);
        }

        if (score >= PreflopCallScore && legal.Contains(PlayerAction.Call))
        {
            if (potMath.BreakEvenEquity <= 0.40 || potMath.StackToPotRatio >= 4.0)
                return new BotAction(PlayerAction.Call);
        }
        
        if (legal.Contains(PlayerAction.Fold)) return new BotAction(PlayerAction.Fold);
        if (legal.Contains(PlayerAction.Call)) return new BotAction(PlayerAction.Call);
        return new BotAction(PlayerAction.Fold);
    }

    private static PotMath BuildPotMathFor(Table t, int seatIndex)
    {
        var me = t.Players[seatIndex];
        var opp = t.Players
            .Where(p => p.SeatIndex != seatIndex && p is { IsOut: false, HasFolded: false })
            .Select(p => p.Stack)
            .DefaultIfEmpty(0)
            .Max();

        return PotMath.Build(t.Pot, t.CurrentBet, me.CommittedThisHand, me.Stack, opp);
    }

    private static (int hi, int lo, bool suited, int gap) InspectHole(IReadOnlyList<Card> hole)
    {
        var r1 = (int)hole[0].Rank;
        var r2 = (int)hole[1].Rank;
        var hi = Math.Max(r1, r2);
        var lo = Math.Min(r1, r2);
        var suited = hole[0].Suit == hole[1].Suit;
        var gap = Math.Abs(hi - lo) - 1;
        return (hi, lo, suited, gap);
    }

    private static double ScorePreflop(int hi, int lo, bool suited, int gap)
    {
        // Rank base
        double score = hi * 4 + lo;

        // Pocket pair boost
        if (hi == lo) score += 40 + (hi - 2) * 2;

        // Suited / connected
        if (suited) score += 10;
        if (gap == 0) score += 8;
        else if (gap == 1) score += 4;

        // Broadways
        if (hi >= 13 && lo >= 10) score += 10; // KQ, AQ, AJ…

        return score;
    }

    private static int Clamp(int x, int low, int high, int step)
    {
        var rounded = (int)Math.Round(x / (double)step) * step;
        if (rounded < low) rounded = low;
        if (rounded > high) rounded = high;
        return rounded;
    }
}