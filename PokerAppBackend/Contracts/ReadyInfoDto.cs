namespace PokerAppBackend.Contracts;

public sealed class ReadyInfoDto
{
    public DateTime DeadlineUtc { get; init; }
    public int[] ReadySeats { get; init; } = Array.Empty<int>();
}
