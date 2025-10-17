namespace PokerAppBackend.Domain;

public sealed class HandRankImprovementProbability
{
    public HandRank TargetHandRank { get; init; }
    public double ThisStreet { get; init; }
    public double ThisAndNextStreetCumulative { get; init; }
}