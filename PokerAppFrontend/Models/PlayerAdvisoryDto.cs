namespace PokerAppFrontend.Models;

public sealed class PlayerAdvisoryDto
{
    public string Street { get; init; } = "";
    public string CurrentHandRank { get; init; } = "";
    public List<CardDto> BestFiveCards { get; init; } = [];
    public double StrengthPercentile { get; init; }
    public string FlushDraw { get; init; } = "None";
    public string StraightDraw { get; init; } = "None";
    public int Overcards { get; init; }
    public List<HandRankImprovementProbabilityDto> HandRankImprovementProbabilityDtos { get; init; } = [];
}