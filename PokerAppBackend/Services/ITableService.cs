using PokerAppBackend.Domain;
using ShowdownResult = PokerAppBackend.Domain.ShowdownResult;

namespace PokerAppBackend.Services;

public interface ITableService
{
    string CreateTable(int playerCount);
    Table Get(string tableCode);
    void JoinAsPlayer(string tableCode, int seatIndex, string name);
    void SetSeatToBot(string tableCode, int seatIndex);
    void StartHand(string tableCode);
    void DealFlop(string tableCode);
    void DealTurn(string tableCode);
    void DealRiver(string tableCode);
    void Check(string tableCode, int seatIndex);
    void Call(string tableCode, int seatIndex);
    void Raise(string tableCode, int seatIndex, int amount);
    FoldResult Fold(string tableCode, int seatIndex);
    ShowdownResult Showdown(string tableCode);
}