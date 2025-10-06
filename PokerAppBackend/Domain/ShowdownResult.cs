namespace PokerAppBackend.Domain;

public sealed class ShowdownResult
{
    public int[] Winners { get; init; } = Array.Empty<int>();
    public List<(Player Player, HandValue HandValue)> Scored { get; init; } = new();
}