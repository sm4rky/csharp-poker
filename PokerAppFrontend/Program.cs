using PokerAppFrontend.Components;
using PokerAppFrontend.Configuration;
using PokerAppFrontend.Services;
using PokerAppFrontend.States;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRadzenComponents();
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));
builder.Services.AddGlobalHttpClient();
builder.Services.AddScoped<RoomClientState>();
builder.Services.AddScoped<IRoomApiClient, RoomApiClient>();
builder.Services.AddScoped<IRoomHubClient, RoomHubClient>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();