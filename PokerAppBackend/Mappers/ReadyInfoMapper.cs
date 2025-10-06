using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class ReadyInfoMapper
{
    public static ReadyInfoDto ToReadyInfoDto(this ReadyInfo readyInfo) => new()
    {
        DeadlineUtc = readyInfo.DeadlineUtc,
        ReadySeats = readyInfo.ReadySeats.Keys.OrderBy(seat => seat).ToArray(),
    };
}