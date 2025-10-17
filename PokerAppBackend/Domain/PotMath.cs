namespace PokerAppBackend.Domain;

public class PotMath
{
    public int Pot { get; init; }
    public int NeededAmountToCall { get; init; } // max(0, CurrentBetAmount - CommittedThisStreet)
    public double PotOdds { get; init; } // ToCall / (Pot + ToCall)
    public double BreakEvenEquity { get; init; } // == PotOdds
    public double StackToPotRatio { get; init; } // Compare the smallest stack with current pot
    public int EffectiveStack { get; init; }

    public static PotMath Build(int pot, int currentBet, int committed, int stack, int opponentStack)
    {
        var neededAmountToCall = Math.Max(0, currentBet - committed);
        var effective = Math.Min(stack, opponentStack);
        var potOdds = neededAmountToCall > 0 ? (double)neededAmountToCall / (pot + neededAmountToCall) : 0.0;
        var stackToPotRatio = pot > 0 ? (double)effective / pot : 0.0;

        return new PotMath
        {
            Pot = pot,
            NeededAmountToCall = neededAmountToCall,
            PotOdds = potOdds,
            BreakEvenEquity = potOdds,
            StackToPotRatio = stackToPotRatio,
            EffectiveStack = effective
        };
    }
}