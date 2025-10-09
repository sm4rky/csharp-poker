namespace PokerAppFrontend.Models;

public sealed class HandResultDto
{
    public PlayerDto Player { get; init; } = new();
    public string HandRank { get; init; } = "";
    public int[] Kickers { get; init; } = [];
}