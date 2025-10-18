using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class TableMapper
{
    public static TableDto ToTableDto(this Table table, int? seatIndex) => new()
    {
        TableCode = table.TableCode,
        Round = table.Round,
        Street = table.Street.ToString(),
        Players = table.Players
            .Select(player => player.ToPlayerDto(seatIndex))
            .ToList(),
        Community = table.Community.Select(card => card.ToCardDto()).ToList(),
        Dealer = table.Dealer,
        SmallBlind = table.SmallBlind,
        BigBlind = table.BigBlind,
        CurrentSeatToAct = table.CurrentSeatToAct ?? -1,
        PreviousSeatToAct = table.PreviousSeatToAct ?? -1,
        ClosingSeat = table.ClosingSeat ?? -1,
        Pot = table.Pot,
        SmallBlindAmount = table.CurrentBlindLevel.SmallBlindAmount,
        BigBlindAmount = table.CurrentBlindLevel.BigBlindAmount,
        CurrentBetAmount = table.CurrentBet,
        LastRaiseSize = table.LastRaiseSize,
        BoardAdvisory = table.BoardAdvisory?.ToBoardAdvisoryDto()
    };
}