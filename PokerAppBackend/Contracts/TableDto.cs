namespace PokerAppBackend.Contracts;

public sealed class TableDto
{
    public string TableCode { get; init; } = "";
    public string Street { get; init; } = "";
    public List<PlayerDto> Players { get; init; } = new();
    public List<CardDto> Community { get; init; } = new();
}