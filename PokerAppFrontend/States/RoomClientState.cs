namespace PokerAppFrontend.States;

public class RoomClientState
{
    public string? LastTableCode { get; set; }
    public string? LastPlayerName { get; set; }
    public int LastSeat { get; set; } = -1;
    public string? PlayerToken { get; set; }

    public void Reset()
    {
        LastTableCode = null;
        LastPlayerName = null;
        LastSeat = -1;
        PlayerToken = null;
    }
}