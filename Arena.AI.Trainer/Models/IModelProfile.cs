using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.QFolder;
using Arena.AI.Trainer.Training;
using Microsoft.Extensions.DependencyInjection;

namespace Arena.AI.Trainer.Models;

public interface IModelProfile : IAsyncDisposable
{
    string ModelName { get; }
    TrainingConfig DefaultConfig { get; }

    Task InitializeAsync(string dbPath, TextWriter trainingLog);
    void ConfigureServices(IServiceCollection services);

    IRealtimePlayer CreateTrainingPlayer(double epsilon, Random rng);
    IRealtimePlayer CreateSnapshotPlayer(double epsilon, Random rng);
    IRealtimePlayer CreateBaselineOpponent();

    void EnqueueResult(QBattleResultBuffer buffer, BattleResult result, bool isSelfPlay);

    Task UpdateSnapshotAsync();
    void InvalidateCaches();

    Task<IValidationContext> CreateValidationContextAsync(string dbPath, int episode);
    Task SavePrevValidationSnapshotAsync();
    Task DropEvalTablesAsync(IEnumerable<string> tableNames);
    Task CleanupTablesAsync();
}

public interface IValidationContext : IAsyncDisposable
{
    IRealtimePlayer CreateCurrentPlayer(Random rng);
    IRealtimePlayer CreateBaselineOpponent();
    IRealtimePlayer CreatePreviousPlayer(Random rng);
    IReadOnlyList<string> TablesToCleanUp { get; }
}
