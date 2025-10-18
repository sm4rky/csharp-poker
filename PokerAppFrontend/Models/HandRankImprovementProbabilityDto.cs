namespace PokerAppFrontend.Models;

public sealed class HandRankImprovementProbabilityDto
{
    public string TargetHandRank { get; set; }
    public double ThisStreet { get; init; }
    public double ThisAndNextStreetCumulative { get; init; }
}