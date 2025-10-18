using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class PlayerMapper
{
    public static PlayerDto ToPlayerDto(this Player player, int? seatIndex)
    {
        var canSeeInfo = seatIndex == null || player.SeatIndex == seatIndex;

        return new PlayerDto
        {
            SeatIndex = player.SeatIndex,
            Name = player.Name,
            IsBot = player.IsBot,
            HasFolded = player.HasFolded,

            Hole = canSeeInfo
                ? player.Hole.Select(card => card.ToCardDto()).ToList()
                : new List<CardDto>(),

            LegalActions = player.LegalActions.Select(a => a.ToString()).ToList(),
            LastestAction = player.LatestAction.ToString(),

            IsOut = player.IsOut,
            Stack = player.Stack,
            CommittedThisStreet = player.CommittedThisStreet,
            CommittedThisHand = player.CommittedThisHand,
            PlayerAdvisory = canSeeInfo
                ? player.PlayerAdvisory?.ToPlayerAdvisoryDto()
                : null
        };
    }
}