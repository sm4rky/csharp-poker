using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class PlayerMapper
{
    public static PlayerDto ToPlayerDto(this Player player, int? seatIndex)
    {
        var canSeeHole = seatIndex == null || player.SeatIndex == seatIndex;
        return new PlayerDto()
        {
            SeatIndex = player.SeatIndex,
            Name = player.Name,
            IsBot = player.IsBot,
            HasFolded = player.HasFolded,
            Hole = canSeeHole
                ? player.Hole.Select(card => card.ToCardDto()).ToList()
                : new List<CardDto>(),
            LegalActions = player.LegalActions.Select(action => action.ToString()).ToList()
        };
    }
}