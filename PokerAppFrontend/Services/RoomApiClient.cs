using Microsoft.AspNetCore.WebUtilities;
using PokerAppFrontend.Models.Responses;

namespace PokerAppFrontend.Services;

public sealed class RoomApiClient(HttpClient http) : IRoomApiClient
{
    private readonly HttpClient _http = http;
    private const string BaseUrl = "/api/table";

    public async Task<CreateTableResponse> CreateTableAsync(int playerCount)
    {
        var query = new Dictionary<string, string?>
        {
            ["playerCount"] = playerCount.ToString(),
        };

        var url = QueryHelpers.AddQueryString($"{BaseUrl}/create", query);

        var response = await _http.PostAsync(url, null);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        return result ?? throw new InvalidOperationException("Invalid response");
    }
}