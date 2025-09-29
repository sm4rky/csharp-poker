namespace PokerAppBackend.Contracts;

public sealed class DrawManyResponse
{
    public List<CardDto> Cards { get; init; } = new();
    public int Remaining { get; init; }
}