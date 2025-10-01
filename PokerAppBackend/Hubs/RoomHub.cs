using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PokerAppBackend.Mappers;
using PokerAppBackend.Records;
using PokerAppBackend.Services;

namespace PokerAppBackend.Hubs;

public class RoomHub(ITableService tableService) : Hub
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(12);

    //connectionId -> (tableCode, seatIndex, token)
    private static readonly ConcurrentDictionary<string, (string TableCode, int? SeatIndex, string? Token)>
        ConnectionMap = new();

    // playerToken -> (tableCode, seatIndex, name, lastSeen, isConnected)
    private static readonly ConcurrentDictionary<string, PlayerSession> PlayerMap = new();

    private static readonly ConcurrentDictionary<string, CancellationTokenSource> PendingKicksMap = new();

    public async Task JoinRoom(string tableCode)
    {
        try
        {
            ConnectionMap[Context.ConnectionId] = (tableCode, null, null);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"table:{tableCode}");
            var table = tableService.Get(tableCode);
            await Clients.Caller.SendAsync("TableState", table.ToTableDto(null));
            await BroadcastTable(tableCode);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
            ConnectionMap.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"table:{tableCode}");
            throw;
        }
    }

    public async Task<string> JoinAsPlayer(string tableCode, int seatIndex, string name)
    {
        tableService.JoinAsPlayer(tableCode, seatIndex, name);

        var token = Guid.NewGuid().ToString("N");
        PlayerMap[token] = new PlayerSession(tableCode, seatIndex, name)
        {
            IsConnected = true,
            LastSeenUtc = DateTime.UtcNow
        };

        ConnectionMap[Context.ConnectionId] = (tableCode, seatIndex, token);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"table:{tableCode}");
        await BroadcastTable(tableCode);

        return token;
    }

    public async Task LeaveSeat(string token)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            return;

        var tableCode = playerInfo.TableCode;
        var seatIndex = playerInfo.SeatIndex;

        tableService.SetSeatToBot(tableCode, seatIndex);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"table:{tableCode}");
        PlayerMap.TryRemove(token, out _);
        ConnectionMap.TryRemove(Context.ConnectionId, out _);
        await BroadcastTable(tableCode);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionMap.TryRemove(Context.ConnectionId, out var connectionInfo))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"table:{connectionInfo.TableCode}");

            // Trigger pending rejoin
            if (!string.IsNullOrEmpty(connectionInfo.Token) &&
                PlayerMap.TryGetValue(connectionInfo.Token!, out var playerInfo))
            {
                PlayerMap[connectionInfo.Token!] =
                    playerInfo with { IsConnected = false, LastSeenUtc = DateTime.UtcNow };

                var cancellationTokenSource = new CancellationTokenSource();
                PendingKicksMap[connectionInfo.Token!] = cancellationTokenSource;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(Grace, cancellationTokenSource.Token);
                        tableService.SetSeatToBot(playerInfo.TableCode, playerInfo.SeatIndex);
                        PlayerMap.TryRemove(connectionInfo.Token!, out _);
                        await BroadcastTable(playerInfo.TableCode);
                    }
                    catch (OperationCanceledException)
                    {
                        /* rejoined */
                    }
                    finally
                    {
                        PendingKicksMap.TryRemove(connectionInfo.Token!, out _);
                    }
                });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Rejoin(string token)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            throw new HubException("Invalid player token.");

        if (PendingKicksMap.TryRemove(token, out var cancellationTokenSource)) cancellationTokenSource.Cancel();

        PlayerMap[token] = playerInfo with { IsConnected = true, LastSeenUtc = DateTime.UtcNow };

        ConnectionMap[Context.ConnectionId] =
            (TableCode: playerInfo.TableCode, SeatIndex: playerInfo.SeatIndex, Token: token);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"table:{playerInfo.TableCode}");

        await BroadcastTable(playerInfo.TableCode);
    }

    public async Task StartHand(string tableCode)
    {
        tableService.StartHand(tableCode);
        await BroadcastTable(tableCode);
    }

    public async Task DealFlop(string tableCode)
    {
        tableService.DealFlop(tableCode);
        await BroadcastTable(tableCode);
    }

    public async Task DealTurn(string tableCode)
    {
        tableService.DealTurn(tableCode);
        await BroadcastTable(tableCode);
    }

    public async Task DealRiver(string tableCode)
    {
        tableService.DealRiver(tableCode);
        await BroadcastTable(tableCode);
    }

    private async Task BroadcastTable(string tableCode)
    {
        var table = tableService.Get(tableCode);

        foreach (var (connectionId, info) in ConnectionMap.ToArray())
        {
            if (info.TableCode != tableCode) continue;
            await Clients.Client(connectionId).SendAsync("TableState", table.ToTableDto(info.SeatIndex));
        }
    }
}