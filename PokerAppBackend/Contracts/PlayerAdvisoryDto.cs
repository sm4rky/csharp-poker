namespace PokerAppBackend.Contracts;

public sealed class PlayerAdvisoryDto
{
    public string Street { get; init; } = "";
    public string CurrentHandRank { get; init; } = "";
    public List<CardDto> BestFiveCards { get; init; } = [];
    public double StrengthPercentile { get; init; } // 0..100
    public string FlushDraw { get; init; } = "None"; // None | FourFlush | Backdoor
    public string StraightDraw { get; init; } = "None"; // None | OESD | Gutshot | Backdoor
    public int Overcards { get; init; } // 0,1,2
    public List<HandRankImprovementProbabilityDto> HandRankImprovementProbabilityDtos { get; init; } = [];
    public PotMathDto PotMath { get; init; } = new();
}