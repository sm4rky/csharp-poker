using PokerAppFrontend.Models;

namespace PokerAppFrontend.Services;

public interface IRoomHubClient : IAsyncDisposable
{
    bool IsConnected { get; }
    string? TableCode { get; }
    string? Token { get; }

    event Action<TableDto>? TableState;
    event Action<DefaultWinResultDto>? DefaultWinResult;
    event Action<ShowdownResultDto>? ShowdownResult;
    event Action<ReadyInfoDto>? ReadyState;
    event Action<PlayerDto>? LastStanding;
    event Action<string>? Error;

    Task JoinRoomAsync(string tableCode);
    Task<string> JoinAsPlayerAsync(string tableCode, int seatIndex, string name);
    Task DisconnectAsync();
    Task LeaveSeatAsync(string token);
    Task RejoinAsync(string token);
    Task StartHandAsync(string tableCode);
    Task CheckAsync();
    Task CallAsync();
    Task RaiseAsync(int amount);
    Task FoldAsync();
    Task ReadyForNextMatchAsync();
}