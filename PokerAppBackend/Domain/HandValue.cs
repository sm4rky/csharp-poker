namespace PokerAppBackend.Domain;

public readonly record struct HandValue(HandRank Rank, int K1, int K2, int K3, int K4, int K5) : IComparable<HandValue>
{
    public int CompareTo(HandValue other)
    {
        return Comparer<(int, int, int, int, int, int)>.Default.Compare(
            ((int)Rank, K1, K2, K3, K4, K5),
            ((int)other.Rank, other.K1, other.K2, other.K3, other.K4, other.K5)
        );
    }

    public override string ToString() => Rank.ToString();

    public string ToDebugString() => $"{Rank}, {K1}, {K2}, {K3}, {K4}, {K5}";
}