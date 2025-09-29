using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class CardMapper
{
    public static CardDto ToCardDto(this Card card) => new()
    {
        Rank = card.Rank.ToString(),
        Suit = card.Suit.ToString(),
        Text = card.ToString()
    };
}