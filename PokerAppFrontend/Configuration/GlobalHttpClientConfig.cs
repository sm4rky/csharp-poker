using Microsoft.Extensions.Options;

namespace PokerAppFrontend.Configuration;

public static class GlobalHttpClientConfig
{
    public static void AddGlobalHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient("Default", (sp, client) =>
        {
            var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
            client.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return factory.CreateClient("Default");
        });
    }
}