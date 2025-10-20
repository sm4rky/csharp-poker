using PokerAppBackend.Hubs;
using PokerAppBackend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials();
    });
});

builder.Services.AddSingleton<IDeckService, DeckService>();
builder.Services.AddSingleton<IEvaluateHandService, EvaluateHandService>();
builder.Services.AddSingleton<IStreetAdvisorService, StreetAdvisorService>();
builder.Services.AddSingleton<IBotService, BotService>();
builder.Services.AddSingleton<ITableService, TableService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("AllowAll");
app.MapHub<RoomHub>("/roomhub");

app.Run();