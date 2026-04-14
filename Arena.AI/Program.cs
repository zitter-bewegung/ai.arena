using Arena.AI.Services;
using Arena.AI.Core;
using Arena.AI.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Battle result persistence pipeline
builder.Services
    .AddSingleton<BattleResultBuffer>()
    .AddSingleton<DuckDbBattleRepository>()
    .AddHostedService<BattleResultsFlushService>();

var app = builder.Build();

ActiveBattlesManager.Init(app.Services);

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
