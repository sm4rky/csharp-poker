using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class TableMapper
{
    public static TableDto ToTableDto(this Table table, int? seatIndex) => new()
    {
        TableCode = table.TableCode,
        Street = table.Street.ToString(),
        Players = table.Players
            .Select(player => player.ToPlayerDto(seatIndex))
            .ToList(),
        Community = table.Community.Select(card => card.ToCardDto()).ToList()
    };
}