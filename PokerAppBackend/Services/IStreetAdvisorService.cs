using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public interface IStreetAdvisorService
{
    BoardAdvisory BuildBoardAdvisory(Table table);
    PlayerAdvisory? BuildPlayerAdvisory(Table table, int seatIndex);
}