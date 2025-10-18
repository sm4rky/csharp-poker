using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public sealed class StreetAdvisorService(IEvaluateHandService evaluateHandService) : IStreetAdvisorService
{
    public BoardAdvisory? BuildBoardAdvisory(Table table)
    {
        var street = table.Street;
        var community = table.Community;
        if (community.Count < 3) return null;
        var analysis = HandAnalysis.BuildBoard(community);

        // Paired: There existed at least a pair
        var paired = Enumerable.Range(2, 13).Any(r => analysis.RankCount(r) >= 2);

        // Monotone: when all community cards have the same suit
        var distinctSuits = Enumerable.Range(0, 4).Count(s => analysis.SuitCount(s) > 0);
        var monotone = distinctSuits == 1;

        // TwoTone: there are 2 suits on community
        var twoTone = distinctSuits == 2 && !monotone;

        // Connected/Wet
        var ranksPresent = analysis.RankMask; // bitmask 2(off) 3(off)..A(on)
        var connected = HasStraight(ranksPresent, minLen: 3);

        //calculate flush threat (0..3)
        var flushThreatScore = CalculateFlushThreatScore(street, analysis);

        //calculate straight threat (0..3)
        var straightThreatScore = CalculateStraightThreatScore(ranksPresent, street);

        var texture = monotone ? BoardTexture.Monotone
            : twoTone ? BoardTexture.TwoTone
            : connected ? BoardTexture.Connected
            : paired ? BoardTexture.Paired
            : BoardTexture.Dry;

        // Wet if both connected and twotone
        if ((straightThreatScore >= 2 && flushThreatScore >= 1) || (connected && twoTone))
            texture = BoardTexture.Wet;

        return new BoardAdvisory
        {
            Street = street,
            Texture = texture,
            Paired = paired,
            Monotone = monotone,
            StraightThreatScore = straightThreatScore, // 0..3
            FlushThreatScore = flushThreatScore, // 0..3
            TripsPossibleOnBoard =
                paired // if there is at least a pair, there is possible of a three of a kind or full house
        };
    }

    public PlayerAdvisory? BuildPlayerAdvisory(Table table, int seatIndex)
    {
        var street = table.Street;
        var community = table.Community;
        var player = table.Players[seatIndex];
        var hole = player.Hole;
        if (community.Count < 3 || hole.Count < 2) return null;
        var combinedCards = hole.Concat(community).ToArray();

        //Current hand value, rank
        var handValue = evaluateHandService.EvaluateHand(hole, community);
        var handRank = handValue.Rank;

        //Best five cards
        var bestFiveCards = combinedCards.OrderByDescending(c => c.Rank).Take(5).ToList();

        //Get Strength Percentile
        var strengthPercentile = CalculateStrengthPercentile(hole, community);

        var analysis = HandAnalysis.BuildCombined(combinedCards);

        //Get Flush Draw Type
        var flushDrawType = GetFlushDrawType(street, analysis);

        //Get Straight Draw Type
        var straightDrawType = GetStraightDrawType(street, analysis.RankMask);

        //Get Overcards
        var overcards = ComputeOvercards(hole, community);

        //Get Hand Rank Improvement Probability
        var handRankImprovementProbability = CalculateHandRankImprovementProbabilities(street, hole, community);

        //Get Pot math
        var aliveOpponentStacks = table.Players
            .Where(p => p.SeatIndex != player.SeatIndex && p is { IsOut: false, HasFolded: false })
            .Select(p => p.Stack)
            .DefaultIfEmpty(0)
            .ToArray();

        var potMath = PotMath.Build(table.Pot, table.CurrentBet, player.CommittedThisHand, player.Stack,
            aliveOpponentStacks.Max());


        return new PlayerAdvisory
        {
            Street = street,
            CurrentHandRank = handRank,
            BestFiveCards = bestFiveCards,
            StrengthPercentile = strengthPercentile,
            FlushDraw = flushDrawType,
            StraightDraw = straightDrawType,
            Overcards = overcards,
            HandRankImprovementProbabilities = handRankImprovementProbability,
            PotMath = potMath,
        };
    }

    private static bool HasStraight(ushort rankMask, int minLen)
    {
        var run = 0;
        for (var r = 2; r <= 14; r++)
        {
            var on = (rankMask & (1 << (r - 2))) != 0;
            if (on)
            {
                run++;
                if (run >= minLen) return true;
            }
            else run = 0;
        }

        if (minLen > 4) return false;
        var hasAce = (rankMask & (1 << (14 - 2))) != 0;
        var twoToFive = true;
        for (var rank = 2; rank <= 5; rank++)
        {
            if ((rankMask & (1 << (rank - 2))) == 0)
                twoToFive = false;
        }

        return hasAce && twoToFive;
    }

    // Flop : c=3 ->2, c=2 ->1, else 0
    // Turn : c=4 ->3, c=3 ->2, c=2 ->1, else 0
    // River: c=5 ->3, c=4 ->2, c=3 ->1, else 0
    private static int CalculateFlushThreatScore(Street street, HandAnalysis analysis)
    {
        var maxSuitOnBoard = 0;
        for (var s = 0; s < 4; s++)
            maxSuitOnBoard = Math.Max(maxSuitOnBoard, analysis.SuitCount(s));

        return street switch
        {
            Street.Flop => (maxSuitOnBoard >= 3 ? 2 : (maxSuitOnBoard >= 2 ? 1 : 0)),
            Street.Turn => (maxSuitOnBoard >= 4 ? 3 : (maxSuitOnBoard >= 3 ? 2 : (maxSuitOnBoard >= 2 ? 1 : 0))),
            Street.River => (maxSuitOnBoard >= 5 ? 3 : (maxSuitOnBoard >= 4 ? 2 : (maxSuitOnBoard >= 3 ? 1 : 0))),
            _ => 0
        };
    }

    // Flop : k>=3 ->1
    // Turn : k>=4 ->2; k>=3 ->1
    // River: k>=5 ->3; k>=4 ->2; k>=3 ->1
    private static int CalculateStraightThreatScore(ushort rankMask, Street street)
    {
        var straightThreatScore = 0;
        foreach (var (lowRank, highRank) in GetFiveRanksRange())
        {
            var count = CountRanksInRange(rankMask, lowRank, highRank);
            var currentScore = street switch
            {
                Street.Flop => (count >= 3 ? 1 : 0),
                Street.Turn => (count >= 4 ? 2 : (count >= 3 ? 1 : 0)),
                Street.River => (count >= 5 ? 3 : (count >= 4 ? 2 : (count >= 3 ? 1 : 0))),
                _ => 0
            };
            straightThreatScore = Math.Max(straightThreatScore, currentScore);
        }

        return straightThreatScore;
    }

    private static IEnumerable<(int lowRank, int highRank)> GetFiveRanksRange()
    {
        for (var lowRank = 2; lowRank <= 10; lowRank++) yield return (lowRank, lowRank + 5 - 1);
        yield return (14, 5);
    }

    private static int CountRanksInRange(ushort mask, int lowRank, int highRank)
    {
        var count = 0;
        if (lowRank <= highRank)
        {
            for (var r = lowRank; r <= highRank; r++)
                if ((mask & (1 << (r - 2))) != 0)
                    count++;
        }
        else
        {
            if ((mask & (1 << (14 - 2))) != 0) count++; // Has Ace
            for (var r = 2; r <= 5; r++)
                if ((mask & (1 << (r - 2))) != 0)
                    count++;
        }

        return count;
    }

    private static bool HasEnoughCount(ushort mask, int requestCount)
    {
        foreach (var (lo, hi) in GetFiveRanksRange())
        {
            var countRanksInWindow = CountRanksInRange(mask, lo, hi);
            if (countRanksInWindow == requestCount) return true;
        }

        return false;
    }

    private static FlushDrawType GetFlushDrawType(Street street, HandAnalysis analysis)
    {
        var maxSuitCount = Enumerable.Range(0, 4)
            .Select(analysis.SuitCount)
            .Max();

        return street switch
        {
            Street.Flop => (maxSuitCount >= 4
                ? FlushDrawType.FourFlush
                : (maxSuitCount >= 3 ? FlushDrawType.Backdoor : FlushDrawType.None)),
            Street.Turn => (maxSuitCount >= 4 ? FlushDrawType.FourFlush : FlushDrawType.None),
            _ => FlushDrawType.None
        };
    }

    private static StraightDrawType GetStraightDrawType(Street street, ushort rankMask)
    {
        if (street == Street.River || HasStraight(rankMask, 5)) return StraightDrawType.None;

        if (HasStraight(rankMask, 4)) return StraightDrawType.Oesd;

        if (HasEnoughCount(rankMask, 4)) return StraightDrawType.Gutshot;

        if (street == Street.Flop && HasEnoughCount(rankMask, 3)) return StraightDrawType.Backdoor;

        return StraightDrawType.None;
    }

    private static int ComputeOvercards(IReadOnlyList<Card> hole, IReadOnlyList<Card> community)
    {
        var highestBoardRank = community.Max(c => (int)c.Rank);
        var over = 0;
        if ((int)hole[0].Rank > highestBoardRank) over++;
        if ((int)hole[1].Rank > highestBoardRank) over++;
        return over; // 0..2
    }

    private static List<Card> GetUnseenCards(IReadOnlyList<Card> combinedCards)
    {
        var seenCards = new HashSet<Card>(combinedCards);
        var deck = Deck.Standard();
        var unseenCards = new List<Card>(52 - combinedCards.Count);
        unseenCards.AddRange(deck.Cards.Where(c => !seenCards.Contains(c)));
        return unseenCards;
    }

    private double CalculateStrengthPercentile(
        IReadOnlyList<Card> hole,
        IReadOnlyList<Card> community)
    {
        var currentHandValue = evaluateHandService.EvaluateHand(hole, community);
        var unseen = GetUnseenCards(hole.Concat(community).ToArray());
        double wins = 0;
        double ties = 0;
        double total = 0;
        for (var i = 0; i < unseen.Count; i++)
        for (var j = i + 1; j < unseen.Count; j++)
        {
            total++;
            var oppV = evaluateHandService.EvaluateHand([unseen[i], unseen[j]], community);
            var compareTo = currentHandValue.CompareTo(oppV);
            switch (compareTo)
            {
                case > 0:
                    wins++;
                    break;
                case 0:
                    ties++;
                    break;
            }
        }

        return total > 0 ? 100.0 * (wins + 0.5 * ties) / total : 0.0;
    }

    private IReadOnlyList<HandRankImprovementProbability> CalculateHandRankImprovementProbabilities(
        Street street,
        IReadOnlyList<Card> hole,
        IReadOnlyList<Card> community)
    {
        if (street is not Street.Flop and not Street.Turn) return [];

        var currentHandValue = evaluateHandService.EvaluateHand(hole, community);
        var unseen = GetUnseenCards(hole.Concat(community).ToArray());

        var nextTurnCounts = new Dictionary<HandRank, int>();
        var nextTwoTurnCounts = new Dictionary<HandRank, int>();
        var twoUnseenCombinationCount = 0;

        foreach (var hv in unseen
                     .Select(c => evaluateHandService.EvaluateHand(hole, community.Append(c).ToArray()))
                     .Where(hv => hv.CompareTo(currentHandValue) > 0))
        {
            nextTurnCounts[hv.Rank] = nextTurnCounts.GetValueOrDefault(hv.Rank) + 1;
        }

        if (street == Street.Flop)
        {
            for (var i = 0; i < unseen.Count; i++)
            {
                var c1 = unseen[i];
                for (var j = 0; j < unseen.Count; j++)
                {
                    if (j == i) continue;
                    twoUnseenCombinationCount++;
                    var c2 = unseen[j];
                    var hv = evaluateHandService.EvaluateHand(hole, community.Append(c1).Append(c2).ToArray());
                    if (hv.CompareTo(currentHandValue) > 0)
                        nextTwoTurnCounts[hv.Rank] = nextTwoTurnCounts.GetValueOrDefault(hv.Rank) + 1;
                }
            }
        }
        else
        {
            twoUnseenCombinationCount = 0;
            nextTwoTurnCounts.Clear();
        }

        var keys = street == Street.Flop
            ? nextTurnCounts.Keys.Intersect(nextTwoTurnCounts.Keys)
            : nextTurnCounts.Keys;

        var unseenCount = unseen.Count;
        var result = new List<HandRankImprovementProbability>();

        foreach (var r in keys.OrderBy(r => r))
        {
            var cThis = nextTurnCounts.GetValueOrDefault(r);
            var cCum = nextTwoTurnCounts.GetValueOrDefault(r);

            var pThis = unseenCount > 0 ? 100.0 * cThis / unseenCount : 0.0;
            var pCum = 0.0;
            if (street == Street.Flop)
                pCum = twoUnseenCombinationCount > 0 ? 100.0 * cCum / twoUnseenCombinationCount : 0.0;

            if (pThis > 0.0 || pCum > 0.0)
            {
                result.Add(new HandRankImprovementProbability
                {
                    TargetHandRank = r,
                    ThisStreet = pThis,
                    ThisAndNextStreetCumulative = pCum
                });
            }
        }

        return result;
    }
}