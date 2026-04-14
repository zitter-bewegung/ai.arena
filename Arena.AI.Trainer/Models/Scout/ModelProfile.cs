using Arena.AI.Core;
using Arena.AI.Core.Logic;
using Arena.AI.Core.Models;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.RealtimePlayers;
using Arena.AI.QFolder;
using Arena.AI.Trainer.Training;
using Microsoft.Extensions.DependencyInjection;

namespace Arena.AI.Trainer.Models.Scout;

public class ModelProfile : IModelProfile
{
    private static readonly TableSchema Schema = new();

    public string ModelName => "scout";

    public TrainingConfig DefaultConfig => new()
    {
        CurriculumFraction = 1.0,
        StartSimpleProp = 1.0,
        FinalSimpleProp = 1.0,
    };

    private DuckDbQRepository<QStateAction> _writeRepo = null!;
    private DuckDbQRepository<QStateAction>[] _trainingRepos = null!;
    private DuckDbQRepository<QStateAction>[] _snapshotRepos = null!;
    private CachedQRepository[] _trainingPool = null!;
    private CachedQRepository[] _snapshotPool = null!;
    private RecordExtractor _extractor = null!;
    private string _dbPath = null!;
    private int _poolSize;

    public async Task InitializeAsync(string dbPath, TextWriter trainingLog)
    {
        _dbPath = dbPath;
        _extractor = new RecordExtractor();
        _poolSize = Environment.ProcessorCount;

        _writeRepo = new DuckDbQRepository<QStateAction>(
            dbPath, Schema,
            new DuckDbQRepositoryOptions
            {
                TableName = "q_table",
                UseWeightedAlpha = true,
                CreateIndex = true
            }) { Log = trainingLog };
        await _writeRepo.CreateTableAsync();

        await _writeRepo.CopyTableAsync("q_table", "q_table_snapshot");
        await _writeRepo.CopyTableAsync("q_table", "q_table_prev_validation");

        (_trainingRepos, _trainingPool) = await CreatePoolAsync(dbPath, "q_table");
        (_snapshotRepos, _snapshotPool) = await CreatePoolAsync(dbPath, "q_table_snapshot");
    }

    private async Task<(DuckDbQRepository<QStateAction>[], CachedQRepository[])> CreatePoolAsync(
        string dbPath, string tableName)
    {
        var repos = new DuckDbQRepository<QStateAction>[_poolSize];
        var caches = new CachedQRepository[_poolSize];
        for (int i = 0; i < _poolSize; i++)
        {
            repos[i] = new DuckDbQRepository<QStateAction>(
                dbPath, Schema,
                new DuckDbQRepositoryOptions { TableName = tableName, ReadOnly = true });
            await repos[i].CreateTableAsync();
            caches[i] = new CachedQRepository(repos[i]);
        }
        return (repos, caches);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IQRepository<QStateAction>>(_writeRepo);
        services.AddSingleton<IQRecordsExtractor<QStateAction>>(_extractor);
        services.AddSingleton<QRecordManager<QStateAction>>();
        services.AddSingleton<QBattleResultBuffer>();
        services.AddHostedService<QBattleResultsFlushService<QStateAction>>();
    }

    public IRealtimePlayer CreateTrainingPlayer(double epsilon, Random rng)
    {
        var cache = _trainingPool[Thread.CurrentThread.ManagedThreadId % _poolSize];
        return new Bot(cache, _extractor, epsilon, rng);
    }

    public IRealtimePlayer CreateSnapshotPlayer(double epsilon, Random rng)
    {
        var cache = _snapshotPool[Thread.CurrentThread.ManagedThreadId % _poolSize];
        return new Bot(cache, _extractor, epsilon, rng);
    }

    public IRealtimePlayer CreateBaselineOpponent()
        => new SimplePlayer1();

    public void EnqueueResult(QBattleResultBuffer buffer, BattleResult result, bool isSelfPlay)
    {
        if (!isSelfPlay)
        {
            buffer.Enqueue(result);
            return;
        }
        buffer.Enqueue(StripLabels(result, "TeamB"));
        buffer.Enqueue(StripLabels(result, "TeamA"));
    }

    public async Task UpdateSnapshotAsync()
    {
        await _writeRepo.CopyTableAsync("q_table", "q_table_snapshot");
        InvalidateCaches();
    }

    public void InvalidateCaches()
    {
        foreach (var c in _trainingPool) c.Invalidate();
        foreach (var c in _snapshotPool) c.Invalidate();
    }

    public async Task SavePrevValidationSnapshotAsync()
        => await _writeRepo.CopyTableAsync("q_table", "q_table_prev_validation");

    public async Task<IValidationContext> CreateValidationContextAsync(string dbPath, int episode)
        => await ValidationContext.CreateAsync(dbPath, Schema, _writeRepo, _extractor, episode);

    public async Task DropEvalTablesAsync(IEnumerable<string> tableNames)
    {
        foreach (var table in tableNames)
            await _writeRepo.DropTableAsync(table);
    }

    public async Task CleanupTablesAsync()
        => await _writeRepo.DropTableAsync("q_table_prev_validation");

    public async ValueTask DisposeAsync()
    {
        foreach (var r in _trainingRepos) await r.DisposeAsync();
        foreach (var r in _snapshotRepos) await r.DisposeAsync();
        await CleanupTablesAsync();
        await _writeRepo.DisposeAsync();
    }

    private static BattleResult StripLabels(BattleResult original, string teamPrefixToStrip)
    {
        var prefix = teamPrefixToStrip + "_";
        return new BattleResult
        {
            BattleId = original.BattleId,
            Winner = original.Winner,
            Actions = original.Actions.Select(a => new BattleAction
            {
                UnitName    = a.UnitName,
                UnitType    = a.UnitType,
                ActionType  = a.ActionType,
                Target      = a.Target,
                Amount      = a.Amount,
                Destination = a.Destination,
                Label       = (a.UnitName?.StartsWith(prefix) ?? false) ? null : a.Label,
            }).ToList()
        };
    }
}
