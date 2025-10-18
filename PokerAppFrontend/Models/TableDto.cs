namespace PokerAppFrontend.Models;

public sealed class TableDto
{
    public string TableCode { get; init; } = "";
    public int Round { get; init; }
    public string Street { get; init; } = "";
    public List<PlayerDto> Players { get; init; } = [];
    public List<CardDto> Community { get; init; } = [];
    public int Dealer { get; init; }
    public int SmallBlind { get; init; }
    public int BigBlind { get; init; }
    public int CurrentSeatToAct { get; init; }
    public int PreviousSeatToAct { get; init; }
    public int ClosingSeat { get; init; }
    public int Pot { get; init; }
    public int SmallBlindAmount { get; init; }
    public int BigBlindAmount { get; init; }
    public int CurrentBetAmount { get; init; }
    public int LastRaiseSize { get; init; }
    public BoardAdvisoryDto? BoardAdvisory { get; init; }
}