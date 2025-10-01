namespace PokerAppBackend.Records;

public record PlayerSession(string TableCode, int SeatIndex, string Name)
{
    public bool IsConnected { get; init; }
    public DateTime LastSeenUtc { get; init; }
}