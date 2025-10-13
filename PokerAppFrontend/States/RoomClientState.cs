namespace PokerAppFrontend.States;

public class RoomClientState
{
    public string? LastTableCode { get; set; }
    public string? LastPlayerName { get; set; }
    public string? PlayerToken { get; set; }

    public void Reset()
    {
        LastTableCode = null;
        LastPlayerName = null;
        PlayerToken = null;
    }
}