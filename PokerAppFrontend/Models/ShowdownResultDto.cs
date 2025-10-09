namespace PokerAppFrontend.Models;

public sealed class ShowdownResultDto
{
    public int[] Winners { get; init; } = [];
    public List<HandResultDto> Hands { get; init; } = [];
}