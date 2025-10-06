namespace PokerAppBackend.Contracts;

public sealed class ShowdownResultDto
{
    public int[] Winners { get; init; } = Array.Empty<int>();
    public List<HandResultDto> Hands { get; init; } = new();
}