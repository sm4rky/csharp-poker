using PokerAppFrontend.Models.Responses;

namespace PokerAppFrontend.Services;

public interface IRoomApiClient
{
    Task<CreateTableResponse> CreateTableAsync(int playerCount);
}