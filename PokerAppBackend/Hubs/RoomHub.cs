using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PokerAppBackend.Contracts;
using PokerAppBackend.Domain;
using PokerAppBackend.Mappers;
using PokerAppBackend.Services;

namespace PokerAppBackend.Hubs;

public class RoomHub(ITableService tableService, IBotService botService, IHubContext<RoomHub> hubContext) : Hub
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan BotDelay = TimeSpan.FromSeconds(2);
    
    //Countdown to next match
    private static readonly TimeSpan ReadyWindow = TimeSpan.FromSeconds(15);

    // connectionId -> (tableCode, seatIndex, token)
    private static readonly ConcurrentDictionary<string, (string TableCode, int? SeatIndex, string? Token)>
        ConnectionMap = new();

    // playerToken -> (tableCode, seatIndex, name, lastSeen, isConnected)
    private static readonly ConcurrentDictionary<string, PlayerSession> PlayerMap = new();

    private static readonly ConcurrentDictionary<string, CancellationTokenSource> PendingKicksMap = new();
    private static readonly ConcurrentDictionary<string, ReadyInfo> PendingReadyTableMap = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> PendingBotMap = new();

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
                        await BroadcastTableViaHubContext(playerInfo.TableCode);
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
        if (PendingBotMap.TryRemove(tableCode, out var prevCts))
        {
            prevCts.Cancel();
            prevCts.Dispose();
        }

        tableService.StartHand(tableCode);
        await BroadcastTable(tableCode);
        await ShowdownIfRiverOver(tableCode);
    }

    public async Task DealFlop(string tableCode)
    {
        tableService.DealFlop(tableCode);
        await BroadcastTable(tableCode);
        await ShowdownIfRiverOver(tableCode);
    }

    public async Task DealTurn(string tableCode)
    {
        tableService.DealTurn(tableCode);
        await BroadcastTable(tableCode);
        await ShowdownIfRiverOver(tableCode);
    }

    public async Task DealRiver(string tableCode)
    {
        tableService.DealRiver(tableCode);
        await BroadcastTable(tableCode);
        await ShowdownIfRiverOver(tableCode);
    }

    public async Task Check(string token)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            throw new HubException("Invalid player token.");

        tableService.Check(playerInfo.TableCode, playerInfo.SeatIndex);
        await BroadcastTable(playerInfo.TableCode);
        await ShowdownIfRiverOver(playerInfo.TableCode);
    }

    public async Task Call(string token)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            throw new HubException("Invalid player token.");

        tableService.Call(playerInfo.TableCode, playerInfo.SeatIndex);
        await BroadcastTable(playerInfo.TableCode);
        await ShowdownIfRiverOver(playerInfo.TableCode);
    }

    public async Task Raise(string token, int amount)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            throw new HubException("Invalid player token.");

        tableService.Raise(playerInfo.TableCode, playerInfo.SeatIndex, amount);
        await BroadcastTable(playerInfo.TableCode);
    }

    public async Task Fold(string token)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            throw new HubException("Invalid player token.");

        var foldResult = tableService.Fold(playerInfo.TableCode, playerInfo.SeatIndex);
        await BroadcastTable(playerInfo.TableCode);

        if (foldResult.IsMatchOver)
        {
            await Clients.Group($"table:{playerInfo.TableCode}")
                .SendAsync("DefaultWinResult", new DefaultWinResultDto() { Winner = foldResult.Winner });

            var t = tableService.Get(playerInfo.TableCode);
            var lastStanding = t.GetLastStanding();
            if (lastStanding is not null)
            {
                await Clients.Group($"table:{playerInfo.TableCode}")
                    .SendAsync("LastStanding", lastStanding.ToPlayerDto(null));
                return;
            }

            await BeginNextMatchCountdown(playerInfo.TableCode);
            return;
        }

        await ShowdownIfRiverOver(playerInfo.TableCode);
    }

    private async Task ShowdownIfRiverOver(string tableCode)
    {
        var table = tableService.Get(tableCode);
        if (table.Street != Street.Showdown) return;

        var contenders = table.Players.Count(p =>
            p is { HasFolded: false, IsOut: false, Hole.Count: 2, CommittedThisHand: > 0 });
        if (contenders == 0) return;

        var result = tableService.Showdown(tableCode);

        await BroadcastTable(tableCode);
        await Clients.Group($"table:{tableCode}").SendAsync("ShowdownResult", result.ToShowdownResultDto());
        var lastStanding = table.GetLastStanding();
        if (lastStanding is not null)
        {
            await Clients.Group($"table:{tableCode}")
                .SendAsync("LastStanding", lastStanding.ToPlayerDto(null));
            return;
        }

        await BeginNextMatchCountdown(tableCode);
    }

    private async Task ShowdownIfRiverOverViaHubContext(string tableCode)
    {
        var table = tableService.Get(tableCode);
        if (table.Street != Street.Showdown) return;

        var contenders = table.Players.Count(p =>
            p is { HasFolded: false, IsOut: false, Hole.Count: 2, CommittedThisHand: > 0 });
        if (contenders == 0) return;

        var result = tableService.Showdown(tableCode);

        await BroadcastTableViaHubContext(tableCode);
        await hubContext.Clients.Group($"table:{tableCode}").SendAsync("ShowdownResult", result.ToShowdownResultDto());
        var lastStanding = table.GetLastStanding();
        if (lastStanding is not null)
        {
            await hubContext.Clients.Group($"table:{tableCode}")
                .SendAsync("LastStanding", lastStanding.ToPlayerDto(null));
            return;
        }

        await BeginNextMatchCountdown(tableCode);
    }

    private async Task BroadcastTable(string tableCode)
    {
        var table = tableService.Get(tableCode);

        foreach (var (connectionId, info) in ConnectionMap.ToArray())
        {
            if (info.TableCode != tableCode) continue;
            await Clients.Client(connectionId).SendAsync("TableState", table.ToTableDto(info.SeatIndex));
        }

        await ScheduleBotAction(tableCode);
    }

    private async Task BroadcastTableViaHubContext(string tableCode)
    {
        var table = tableService.Get(tableCode);

        foreach (var (connectionId, info) in ConnectionMap.ToArray())
        {
            if (info.TableCode != tableCode) continue;
            await hubContext.Clients.Client(connectionId).SendAsync("TableState", table.ToTableDto(info.SeatIndex));
        }

        await ScheduleBotAction(tableCode);
    }

    private async Task BeginNextMatchCountdown(string tableCode)
    {
        if (PendingReadyTableMap.TryRemove(tableCode, out var oldReadyInfo))
        {
            oldReadyInfo.CancellationTokenSource.Cancel();
            oldReadyInfo.CancellationTokenSource.Dispose();
        }

        var table = tableService.Get(tableCode);
        var lastStanding = table.GetLastStanding();
        if (lastStanding is not null)
        {
            await hubContext.Clients.Group($"table:{tableCode}")
                .SendAsync("LastStanding", lastStanding.ToPlayerDto(null));
            return;
        }

        var info = new ReadyInfo
        {
            DeadlineUtc = DateTime.UtcNow.Add(ReadyWindow),
        };

        foreach (var botSeat in table.Players.Where(player => player is { IsBot: true, Stack: > 0 })
                     .Select(player => player.SeatIndex))
        {
            info.ReadySeats.TryAdd(botSeat, 0);
        }

        PendingReadyTableMap[tableCode] = info;
        await BroadcastReadyStateViaHubContext(tableCode);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ReadyWindow, info.CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                /* next match starts early */
            }

            //Delay time to next match ends
            try
            {
                if (PendingReadyTableMap.TryRemove(tableCode, out var ready))
                {
                    ready.CancellationTokenSource.Cancel();
                    ready.CancellationTokenSource.Dispose();

                    tableService.StartHand(tableCode);

                    await BroadcastTableViaHubContext(tableCode);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        });
    }

    public async Task ReadyForNextMatch(string token)
    {
        if (!PlayerMap.TryGetValue(token, out var playerInfo))
            throw new HubException("Invalid player token.");

        if (!PendingReadyTableMap.TryGetValue(playerInfo.TableCode, out var readyTableInfo)) return;

        if (readyTableInfo.ReadySeats.TryAdd(playerInfo.SeatIndex, 0))
        {
            await BroadcastReadyState(playerInfo.TableCode);
            var table = tableService.Get(playerInfo.TableCode);
            if (table.Players.Count(p => p.Stack > 0) <= readyTableInfo.ReadySeats.Count)
            {
                if (PendingReadyTableMap.TryRemove(playerInfo.TableCode, out var readyInfo))
                {
                    readyInfo.CancellationTokenSource.Cancel();
                    readyInfo.CancellationTokenSource.Dispose();

                    var champ = table.GetLastStanding();
                    if (champ is not null)
                    {
                        await Clients.Group($"table:{playerInfo.TableCode}")
                            .SendAsync("LastStanding", champ.ToPlayerDto(null));
                        return;
                    }

                    await StartHand(playerInfo.TableCode);
                }
            }
        }
    }

    private Task BroadcastReadyState(string tableCode)
    {
        return !PendingReadyTableMap.TryGetValue(tableCode, out var readyInfo)
            ? Task.CompletedTask
            : Clients.Group($"table:{tableCode}").SendAsync("ReadyState", readyInfo.ToReadyInfoDto());
    }

    private Task BroadcastReadyStateViaHubContext(string tableCode)
    {
        return !PendingReadyTableMap.TryGetValue(tableCode, out var readyInfo)
            ? Task.CompletedTask
            : hubContext.Clients.Group($"table:{tableCode}")
                .SendAsync("ReadyState", readyInfo.ToReadyInfoDto());
    }

    private async Task ScheduleBotAction(string tableCode)
    {
        var table = tableService.Get(tableCode);
        var currentSeatToAct = table.CurrentSeatToAct;
        if (currentSeatToAct is null || table.Street == Street.Showdown) return;
        if (!table.Players[currentSeatToAct.Value].IsBot) return;

        if (PendingBotMap.TryRemove(tableCode, out var old))
        {
            old.Cancel();
            old.Dispose();
        }

        var stamp = (handId: table.HandId, street: table.Street, seat: currentSeatToAct.Value, seq: table.ActionSeq);
        var cts = new CancellationTokenSource();
        PendingBotMap[tableCode] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(BotDelay, cts.Token);

                var t2 = tableService.Get(tableCode);
                if (t2.HandId != stamp.handId || t2.Street != stamp.street ||
                    t2.CurrentSeatToAct != stamp.seat || t2.ActionSeq != stamp.seq) return;

                var player = t2.Players[stamp.seat];
                var pa = player.PlayerAdvisory;
                var ba = t2.BoardAdvisory;
                var bounds = t2.GetRaiseBounds(stamp.seat);

                var act = botService.SetBotAction(t2, stamp.seat, pa, ba, bounds);

                switch (act.Action)
                {
                    case PlayerAction.Check:
                        tableService.Check(tableCode, stamp.seat);
                        break;

                    case PlayerAction.Call:
                        tableService.Call(tableCode, stamp.seat);
                        break;

                    case PlayerAction.Raise:
                        tableService.Raise(tableCode, stamp.seat, act.RaiseTo!.Value);
                        break;

                    case PlayerAction.Fold:
                    {
                        var foldResult = tableService.Fold(tableCode, stamp.seat);
                        await BroadcastTableViaHubContext(tableCode);

                        if (foldResult.IsMatchOver)
                        {
                            await hubContext.Clients.Group($"table:{tableCode}")
                                .SendAsync("DefaultWinResult", new DefaultWinResultDto { Winner = foldResult.Winner });

                            var t3 = tableService.Get(tableCode);
                            var champ = t3.GetLastStanding();
                            if (champ is not null)
                            {
                                await hubContext.Clients.Group($"table:{tableCode}")
                                    .SendAsync("LastStanding", champ.ToPlayerDto(null));
                                return;
                            }

                            await BeginNextMatchCountdown(tableCode);
                            return;
                        }

                        await ShowdownIfRiverOverViaHubContext(tableCode);
                        await ScheduleBotAction(tableCode);
                        return;
                    }

                    case PlayerAction.AllIn:
                    {
                        var maxTo = player.CommittedThisStreet + player.Stack;
                        if (t2.CurrentBet == 0)
                        {
                            tableService.Raise(tableCode, stamp.seat, maxTo);
                        }
                        else
                        {
                            var need = Math.Max(0, t2.CurrentBet - player.CommittedThisStreet);
                            if (player.Stack <= need)
                                tableService.Call(tableCode, stamp.seat);
                            else
                                tableService.Raise(tableCode, stamp.seat, maxTo);
                        }

                        break;
                    }
                }

                await BroadcastTableViaHubContext(tableCode);
                await ShowdownIfRiverOverViaHubContext(tableCode);
                await ScheduleBotAction(tableCode);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await hubContext.Clients.Group($"table:{tableCode}")
                    .SendAsync("Error", $"BotTask error: {ex.Message}");
            }
            finally
            {
                if (PendingBotMap.TryGetValue(tableCode, out var curr) && curr == cts)
                {
                    PendingBotMap.TryRemove(tableCode, out _);
                    cts.Dispose();
                }
            }
        });
    }
}