using Arena.AI.Services;
using Arena.AI.Core;
using Arena.AI.Core.RealtimePlayers;
using Arena.AI.SignalR;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;
using Arena.AI.QFolder;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Battle result persistence pipeline
builder.Services
    .AddSingleton<BattleResultBuffer>()
    .AddSingleton<DuckDbBattleRepository>()
    .AddHostedService<BattleResultsFlushService>();

builder.Services
    .AddSingleton<IQRepository<MinimalQStateAction>, DuckDbRepository>()
    .AddSingleton<IQRecordsExtractor<MinimalQStateAction>, MinimalQRecordExtractor>()
    .AddSingleton<QRecordManager<MinimalQStateAction>>()
    .AddSingleton<QBattleResultBuffer>()
    .AddHostedService<QBattleResultsFlushService>();

var app = builder.Build();

// Initialize Q-learning DuckDB tables (idempotent CREATE TABLE IF NOT EXISTS).
await ((DuckDbRepository)app.Services.GetRequiredService<IQRepository<MinimalQStateAction>>())
    .CreateTableAsync();

ActiveBattlesManager.Init(app.Services);

// Wire the QLearningBot1 factory so live battles can dispatch it via BotList.
BotList.RegisterQLearningBot(() => new QLearningBot1(
    app.Services.GetRequiredService<IQRepository<MinimalQStateAction>>(),
    app.Services.GetRequiredService<IQRecordsExtractor<MinimalQStateAction>>(),
    epsilon: 0.0));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ExternalPlayerHub>("/play");

app.Run();
