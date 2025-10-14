using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class ResultMapper
{
    public static ShowdownResultDto ToShowdownResultDto(this ShowdownResult showdownResult) =>
        new()
        {
            Winners = showdownResult.Winners,
            Hands = showdownResult.Scored
                .OrderBy(tuple => tuple.Player.SeatIndex)
                .Select(tuple => tuple.ToHandResultDto())
                .ToList()
        };
}