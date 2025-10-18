using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;

namespace PokerAppBackend.Mappers;

public static class BoardAdvisoryMapper
{
    public static BoardAdvisoryDto ToBoardAdvisoryDto(this BoardAdvisory board) => new()
    {
        Street = board.Street.ToString(),
        Texture = board.Texture.ToString(),
        Paired = board.Paired,
        Monotone = board.Monotone,
        StraightThreatScore = board.StraightThreatScore,
        FlushThreatScore = board.FlushThreatScore,
        TripsPossibleOnBoard = board.TripsPossibleOnBoard
    };
}