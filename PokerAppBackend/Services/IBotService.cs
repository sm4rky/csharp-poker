using PokerAppBackend.Domain;

namespace PokerAppBackend.Services;

public interface IBotService
{
    BotAction SetBotAction(Table table, int seatIndex, PlayerAdvisory? playerAdvisory, BoardAdvisory? boardAdvisory,
        (bool canRaise, int minTo, int maxTo) raiseBounds);
}