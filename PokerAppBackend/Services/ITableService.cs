using PokerAppBackend.Domain;
using ShowdownResult = PokerAppBackend.Domain.ShowdownResult;

namespace PokerAppBackend.Services;

public interface ITableService
{
    string CreateTable(int playerCount, string name);
    Table Get(string tableCode);
    void JoinAsPlayer(string tableCode, int seatIndex, string name);
    void SetSeatToBot(string tableCode, int seatIndex);
    void StartHand(string tableCode);
    void DealFlop(string tableCode);
    void DealTurn(string tableCode);
    void DealRiver(string tableCode);
    int? Fold(string tableCode, int seatIndex);
    ShowdownResult Showdown(string tableCode);
}