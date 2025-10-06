using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class ResultMapper
{
    public static ShowdownResultDto ToShowdownResultDto(this ShowdownResult showdownResult, int? playerSeat = null) =>
        new()
        {
            Winners = showdownResult.Winners,
            Hands = showdownResult.Scored
                .OrderByDescending(tuple => tuple.HandValue)
                .ThenBy(tuple => tuple.Player.SeatIndex)
                .Select(tuple => tuple.ToHandResultDto(playerSeat))
                .ToList(),
        };
}