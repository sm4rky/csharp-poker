namespace PokerAppBackend.Domain;

public sealed class HandAnalysis
{
    private readonly int[] _rankCounts = new int[15];
    public int RankCount(int rank) => _rankCounts[rank];

    private readonly int[] _suitCounts = new int[4];
    public int SuitCount(int suit) => _suitCounts[suit];

    private ushort _rankMask;
    public ushort RankMask => _rankMask;

    private readonly ushort[] _suitRankMask = new ushort[4];
    public ushort SuitRankMask(int suit) => _suitRankMask[suit];

    private readonly List<Card>[] _bySuit = { new(), new(), new(), new() };
    public IReadOnlyList<Card> SuitCards(int suit) => _bySuit[suit];

    private HandAnalysis()
    {
    }

    public static HandAnalysis BuildCombined(IReadOnlyList<Card> cards)
    {
        if (cards is null || cards.Count < 5 || cards.Count > 7)
            throw new ArgumentException("Combined cards must be 5..7", nameof(cards));

        return BuildInternal(cards);
    }

    public static HandAnalysis BuildBoard(IReadOnlyList<Card> board)
    {
        if (board is null || board.Count < 3 || board.Count > 5)
            throw new ArgumentException("Board cards must be 3..5", nameof(board));

        return BuildInternal(board);
    }

    private static HandAnalysis BuildInternal(IReadOnlyList<Card> cards)
    {
        if (cards is null || cards.Count < 5)
            throw new ArgumentException("Total cards must be >= 5", nameof(cards));

        var analysis = new HandAnalysis();
        foreach (var card in cards)
        {
            var rank = (int)card.Rank;
            var suit = (int)card.Suit;

            if (rank is < 2 or > 14) continue;
            if (suit is < 0 or > 3) continue;

            analysis._rankCounts[rank]++;
            analysis._suitCounts[suit]++;

            analysis._rankMask |= (ushort)(1 << (rank - 2));
            analysis._suitRankMask[suit] |= (ushort)(1 << (rank - 2));

            analysis._bySuit[suit].Add(card);
        }

        return analysis;
    }
}