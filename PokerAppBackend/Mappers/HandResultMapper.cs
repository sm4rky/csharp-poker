using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class HandResultMapper
{
    public static HandResultDto ToHandResultDto(this (Player Player, HandValue HandValue) scored, int? playerSeat) => new()
    {
        Player = scored.Player.ToPlayerDto(playerSeat),
        HandRank = scored.HandValue.ToString(),
        Kickers =
        [
            scored.HandValue.K1,
            scored.HandValue.K2,
            scored.HandValue.K3,
            scored.HandValue.K4,
            scored.HandValue.K5
        ]
    };
}