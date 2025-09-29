namespace PokerAppBackend.Contracts;

public sealed class CardDto
{
    public string Rank { get; init; } = "";
    public string Suit { get; init; } = "";
    public string Text { get; init; } = "";
}