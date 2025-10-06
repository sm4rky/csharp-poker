using System.Collections.Concurrent;

namespace PokerAppBackend.Domain;

public sealed class ReadyInfo
{
    public DateTime DeadlineUtc { get; init; }
    public ConcurrentDictionary<int, byte> ReadySeats { get; } = new();
    public CancellationTokenSource CancellationTokenSource { get; } = new();
}