namespace PokerAppFrontend.Models;

public sealed class ReadyInfoDto
{
    public DateTime DeadlineUtc { get; init; }
    public int[] ReadySeats { get; init; } = [];
}