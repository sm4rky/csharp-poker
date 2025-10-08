namespace PokerAppBackend.Contracts;

public sealed class TableDto
{
    public string TableCode { get; init; } = "";
    public string Street { get; init; } = "";
    public int Dealer { get; init; }
    public int SmallBlind { get; init; }
    public int BigBlind { get; init; }
    public int CurrentSeatToAct { get; init; }
    public int ClosingSeat { get; init; }
    public List<PlayerDto> Players { get; init; } = new();
    public List<CardDto> Community { get; init; } = new();
}