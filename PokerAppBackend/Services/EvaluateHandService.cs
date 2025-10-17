using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public sealed class EvaluateHandService : IEvaluateHandService
{
    public HandValue EvaluateHand(IReadOnlyList<Card> hole, IReadOnlyList<Card> community)
    {
        if (hole is null || community is null)
            throw new ArgumentNullException(null, "Hole or community cannot be null.");

        if (hole.Count != 2)
            throw new ArgumentException("Hole must have 2 cards.", nameof(hole));

        if (community.Count is < 3 or > 5)
            throw new ArgumentOutOfRangeException(nameof(community), "Community must have 3..5 cards.");

        var combinedCards = hole.Concat(community).ToArray();

        var analysis = HandAnalysis.Build(combinedCards);

        //Case Straight Flush
        if (TryStraightFlush(analysis, out var highestStraightFlushRank))
        {
            return new HandValue(HandRank.StraightFlush, highestStraightFlushRank, 0, 0, 0, 0);
        }

        //Determine Rank Groups from combined cards, through analysis class
        var (fourOfAKindRank, threeOfAKindRanksDesc, pairRanksDesc) = CollectRankGroups(analysis);

        //Case Four of a kind
        if (fourOfAKindRank > 0)
        {
            var kickers = GetKickers(combinedCards, new int[] { fourOfAKindRank }, 1);
            return new HandValue(HandRank.FourOfAKind, fourOfAKindRank, kickers[0], 0, 0, 0);
        }

        //Case Full House
        if (threeOfAKindRanksDesc.Count >= 1 && (threeOfAKindRanksDesc.Count >= 2 || pairRanksDesc.Count >= 1))
        {
            var trip = threeOfAKindRanksDesc[0];
            var pair = threeOfAKindRanksDesc.Count >= 2 ? threeOfAKindRanksDesc[1] : pairRanksDesc[0];
            return new HandValue(HandRank.FullHouse, trip, pair, 0, 0, 0);
        }

        //Case Flush
        if (TryFlush(analysis, out var flushRanks))
        {
            return new HandValue(HandRank.Flush, flushRanks[0], flushRanks[1], flushRanks[2], flushRanks[3],
                flushRanks[4]);
        }

        //Case Straight
        if (TryStraight(analysis.RankMask, out var highestStraightRank))
        {
            return new HandValue(HandRank.Straight, highestStraightRank, 0, 0, 0, 0);
        }

        //Case Three of a kind
        if (threeOfAKindRanksDesc.Count >= 1)
        {
            var trip = threeOfAKindRanksDesc[0];
            var kickers = GetKickers(combinedCards, new int[] { trip }, 2);
            return new HandValue(HandRank.ThreeOfAKind, trip, kickers[0], kickers[1], 0, 0);
        }

        //Case Two Pair
        if (pairRanksDesc.Count >= 2)
        {
            var firstPair = pairRanksDesc[0];
            var secondPair = pairRanksDesc[1];
            var kickers = GetKickers(combinedCards, new int[] { firstPair, secondPair }, 1);
            return new HandValue(HandRank.TwoPair, firstPair, secondPair, kickers[0], 0, 0);
        }

        //Case One Pair
        if (pairRanksDesc.Count >= 1)
        {
            var pair = pairRanksDesc[0];
            var kickers = GetKickers(combinedCards, new int[] { pair }, 3);
            return new HandValue(HandRank.OnePair, pair, kickers[0], kickers[1], kickers[2], 0);
        }

        //Case High Card
        var highCardKickers = GetKickers(combinedCards, Array.Empty<int>(), 5);
        return new HandValue(HandRank.HighCard, highCardKickers[0], highCardKickers[1], highCardKickers[2],
            highCardKickers[3], highCardKickers[4]);
    }

    private static bool TryStraightFlush(HandAnalysis handAnalysis, out int highestRank)
    {
        for (var suit = 0; suit < 4; suit++)
        {
            if (handAnalysis.SuitCount(suit) < 5) continue;
            if (TryStraight(handAnalysis.SuitRankMask(suit), out highestRank)) return true;
        }

        highestRank = 0;
        return false;
    }

    private static bool TryFlush(HandAnalysis handAnalysis, out int[] flushRanks)
    {
        for (var suit = 0; suit <= 3; suit++)
        {
            if (handAnalysis.SuitCount(suit) < 5) continue;
            var tempFlushRanks = handAnalysis.SuitCards(suit)
                .OrderByDescending(card => card.Rank)
                .Select(card => (int)card.Rank)
                .Take(5)
                .ToArray();
            flushRanks = tempFlushRanks;
            return true;
        }

        flushRanks = [];
        return false;
    }

    private static bool TryStraight(ushort mask, out int highestRank)
    {
        var hasAce = (mask & (1 << ((int)Rank.Ace - 2))) != 0;
        if (hasAce &&
            (mask & (1 << (5 - 2))) != 0 &&
            (mask & (1 << (4 - 2))) != 0 &&
            (mask & (1 << (3 - 2))) != 0 &&
            (mask & (1 << (2 - 2))) != 0
           )
        {
            highestRank = 5;
            return true;
        }

        for (var rank = 14; rank >= 6; rank--)
        {
            var need =
                (1 << (rank - 2)) |
                (1 << (rank - 3)) |
                (1 << (rank - 4)) |
                (1 << (rank - 5)) |
                (1 << (rank - 6));

            if ((mask & need) != need) continue;

            highestRank = rank;
            return true;
        }

        highestRank = 0;
        return false;
    }

    private static (int fourOfAKindRank, List<int> threeOfAKindRanksDesc, List<int> pairRanksDesc) CollectRankGroups(
        HandAnalysis handAnalysis)
    {
        var fourOfAKindRank = 0;
        var threeOfAKindRanksDesc = new List<int>();
        var pairRanksDesc = new List<int>();

        for (var rank = 14; rank >= 2; rank--)
        {
            var rankCount = handAnalysis.RankCount(rank);
            switch (rankCount)
            {
                case 4:
                    fourOfAKindRank = rank;
                    break;
                case 3:
                    threeOfAKindRanksDesc.Add(rank);
                    break;
                case 2:
                    pairRanksDesc.Add(rank);
                    break;
            }
        }

        return (fourOfAKindRank, threeOfAKindRanksDesc, pairRanksDesc);
    }

    private static int[] GetKickers(
        IReadOnlyList<Card> cards,
        IEnumerable<int> excludedRanks,
        int limit)
    {
        var excludedRanksSet = excludedRanks as ISet<int> ?? new HashSet<int>(excludedRanks);

        return cards
            .Where(card => !excludedRanksSet.Contains((int)card.Rank))
            .Select(card => (int)card.Rank)
            .Distinct() // once for every rank
            .OrderByDescending(r => r)
            .Take(limit) // take enough kickers for particular hand rank
            .ToArray();
    }
}