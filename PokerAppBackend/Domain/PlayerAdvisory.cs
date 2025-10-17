namespace PokerAppBackend.Domain;

public sealed class PlayerAdvisory
{
    public Street Street { get; init; }
    public HandRank CurrentHandRank { get; init; }
    public IReadOnlyList<Card> BestFiveCards { get; init; } = [];
    public double StrengthPercentile { get; init; } // 0..100
    public FlushDrawType FlushDraw { get; init; } // None | FourFlush | Backdoor
    public StraightDrawType StraightDraw { get; init; } // None | OESD | Gutshot | Backdoor
    public int Overcards { get; init; } // 0,1,2
    public IReadOnlyList<HandRankImprovementProbability> HandRankImprovementProbabilities { get; init; } = [];
    public PotMath PotMath { get; init; } = new();
}