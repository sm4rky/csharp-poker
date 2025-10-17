using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class PlayerAdvisoryMapper
{
    public static PlayerAdvisoryDto ToPlayerAdvisoryDto(this PlayerAdvisory playerAdvisory) => new()
    {
        Street = playerAdvisory.Street.ToString(),
        CurrentHandRank = playerAdvisory.CurrentHandRank.ToString(),
        BestFiveCards = playerAdvisory.BestFiveCards.Select(card => card.ToCardDto()).ToList(),
        StrengthPercentile = playerAdvisory.StrengthPercentile,
        FlushDraw = playerAdvisory.FlushDraw.ToString(),
        StraightDraw = playerAdvisory.StraightDraw.ToString(),
        Overcards = playerAdvisory.Overcards,
        HandRankImprovementProbabilityDtos = playerAdvisory.HandRankImprovementProbabilities.Select(p =>
            new HandRankImprovementProbabilityDto
            {
                TargetHandRank = p.TargetHandRank.ToString(),
                ThisStreet = p.ThisStreet,
                ThisAndNextStreetCumulative = p.ThisAndNextStreetCumulative
            }).ToList(),
        PotMath = new PotMathDto
        {
            Pot = playerAdvisory.PotMath.Pot,
            NeededAmountToCall = playerAdvisory.PotMath.NeededAmountToCall,
            PotOdds = playerAdvisory.PotMath.PotOdds,
            BreakEvenEquity = playerAdvisory.PotMath.BreakEvenEquity,
            StackToPotRatio = playerAdvisory.PotMath.StackToPotRatio,
            EffectiveStack = playerAdvisory.PotMath.EffectiveStack
        }
    };
}