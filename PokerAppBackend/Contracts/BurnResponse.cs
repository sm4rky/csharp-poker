namespace PokerAppBackend.Contracts;

public sealed class BurnResponse
{
    public CardDto Card { get; init; } = default!;
    public int Remaining { get; init; }
}