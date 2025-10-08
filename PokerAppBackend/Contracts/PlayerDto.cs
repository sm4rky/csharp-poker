namespace PokerAppBackend.Contracts;

public sealed class PlayerDto
{
    public int SeatIndex { get; init; }
    public string Name { get; init; } = "";
    public bool IsBot { get; init; }
    public bool HasFolded { get; init; }
    public List<CardDto> Hole { get; init; } = new();
    public List<string> LegalActions { get; init; } = new();
}