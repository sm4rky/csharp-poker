namespace PokerAppBackend.Contracts;

public sealed class PotMathDto
{
    public int Pot { get; init; }
    public int NeededAmountToCall { get; init; }
    public double PotOdds { get; init; }
    public double BreakEvenEquity { get; init; }
    public double StackToPotRatio { get; init; }
    public int EffectiveStack { get; init; }
}