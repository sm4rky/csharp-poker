namespace PokerAppFrontend.Services;

public interface IRoomApiClient
{
    Task<string> CreateTableAsync(int playerCount, string name);
}