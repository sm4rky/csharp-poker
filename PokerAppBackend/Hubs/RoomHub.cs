using Microsoft.AspNetCore.SignalR;
using PokerAppBackend.Mappers;
using PokerAppBackend.Services;

namespace PokerAppBackend.Hubs;

public class RoomHub(ITableService tableService) : Hub
{
    public async Task JoinRoom(string tableCode, int seatIndex)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"table:{tableCode}");

        var table = tableService.Get(tableCode);
        var tableDto = table.ToTableDto(seatIndex);
        await Clients.Caller.SendAsync("TableState", tableDto);

        await BroadcastTable(tableCode);
    }

    public async Task LeaveRoom(string tableCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"table:{tableCode}");
        await BroadcastTable(tableCode);
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


        var tableDto = table.ToTableDto(null);
        await Clients.Group($"table:{tableCode}").SendAsync("TableState", tableDto);
    }
}