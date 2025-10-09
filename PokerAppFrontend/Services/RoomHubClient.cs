using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using PokerAppFrontend.Models;

namespace PokerAppFrontend.Services;

public sealed class RoomHubClient(NavigationManager nav) : IRoomHubClient
{
    private readonly NavigationManager _nav = nav;
    private HubConnection? _hub;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;
    public string? TableCode { get; private set; }
    public string? Token { get; private set; }

    public event Action<TableDto>? TableState;
    public event Action<DefaultWinResultDto>? DefaultWinResult;
    public event Action<ShowdownResultDto>? ShowdownResult;
    public event Action<ReadyInfoDto>? ReadyState;
    public event Action<string>? Error;

    public async Task JoinRoomAsync(string tableCode)
    {
        if (_hub is { State: HubConnectionState.Connected } && TableCode == tableCode)
            return;

        if (_hub != null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }

        _hub = new HubConnectionBuilder()
            .WithUrl("https://localhost:7197/roomhub")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<TableDto>("TableState", dto => TableState?.Invoke(dto));
        _hub.On<ReadyInfoDto>("ReadyState", dto => ReadyState?.Invoke(dto));
        _hub.On<DefaultWinResultDto>("DefaultWinResult", dto => DefaultWinResult?.Invoke(dto));
        _hub.On<ShowdownResultDto>("ShowdownResult", dto => ShowdownResult?.Invoke(dto));
        _hub.On<string>("Error", msg => Error?.Invoke(msg));

        await _hub.StartAsync();
        TableCode = tableCode;

        await _hub.InvokeAsync("JoinRoom", tableCode);
        if (!string.IsNullOrWhiteSpace(Token))
        {
            try
            {
                await _hub.InvokeAsync("Rejoin", Token);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    public async Task<string> JoinAsPlayerAsync(string tableCode, int seatIndex, string name)
    {
        await EnsureConnectedAsync(tableCode);
        var token = await _hub!.InvokeAsync<string>("JoinAsPlayer", tableCode, seatIndex, name);
        Token = token;
        return token;
    }

    public async Task LeaveSeatAsync(string token)
    {
        if (_hub is null || string.IsNullOrWhiteSpace(token)) return;
        await _hub.InvokeAsync("LeaveSeat", token);
        Token = null;
    }

    public async Task RejoinAsync(string token)
    {
        await EnsureConnectedAsync(TableCode ?? throw new InvalidOperationException("No table to rejoin."));
        await _hub!.InvokeAsync("Rejoin", token);
        Token = token;
    }

    public async Task StartHandAsync(string tableCode)
    {
        await EnsureConnectedAsync(TableCode ?? throw new InvalidOperationException("No table to start."));
        await _hub!.InvokeAsync("StartHand", tableCode);
    }

    public async Task CheckAsync()
    {
        await EnsurePlayerAsync();
        await _hub!.InvokeAsync("Check", Token!);
    }

    public async Task CallAsync()
    {
        await EnsurePlayerAsync();
        await _hub!.InvokeAsync("Call", Token!);
    }

    public async Task RaiseAsync()
    {
        await EnsurePlayerAsync();
        await _hub!.InvokeAsync("Raise", Token!);
    }

    public async Task FoldAsync()
    {
        await EnsurePlayerAsync();
        await _hub!.InvokeAsync("Fold", Token!);
    }

    public async Task ReadyForNextMatchAsync()
    {
        await EnsurePlayerAsync();
        await _hub!.InvokeAsync("ReadyForNextMatch", Token!);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    private async Task EnsureConnectedAsync(string tableCode)
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected || TableCode != tableCode)
            await JoinRoomAsync(tableCode);
    }

    private Task EnsurePlayerAsync()
    {
        if (_hub is null || _hub.State != HubConnectionState.Connected)
            throw new InvalidOperationException("Hub not connected.");

        return string.IsNullOrWhiteSpace(Token)
            ? throw new InvalidOperationException("Player token missing. Call JoinAsPlayerAsync or RejoinAsync first.")
            : Task.CompletedTask;
    }
}