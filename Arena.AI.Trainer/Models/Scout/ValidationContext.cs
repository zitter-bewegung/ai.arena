using Arena.AI.Core;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.RealtimePlayers;
using Arena.AI.QFolder;

namespace Arena.AI.Trainer.Models.Scout;

public class ValidationContext : IValidationContext
{
    private readonly DuckDbQRepository<QStateAction> _currentRepo;
    private readonly DuckDbQRepository<QStateAction> _prevRepo;
    private readonly CachedQRepository _currentCache;
    private readonly CachedQRepository _prevCache;
    private readonly IQRecordsExtractor<QStateAction> _extractor;

    public IReadOnlyList<string> TablesToCleanUp { get; }

    private ValidationContext(
        DuckDbQRepository<QStateAction> currentRepo,
        DuckDbQRepository<QStateAction> prevRepo,
        CachedQRepository currentCache,
        CachedQRepository prevCache,
        IQRecordsExtractor<QStateAction> extractor,
        IReadOnlyList<string> tablesToCleanUp)
    {
        _currentRepo = currentRepo;
        _prevRepo = prevRepo;
        _currentCache = currentCache;
        _prevCache = prevCache;
        _extractor = extractor;
        TablesToCleanUp = tablesToCleanUp;
    }

    public static async Task<ValidationContext> CreateAsync(
        string dbPath,
        TableSchema schema,
        DuckDbQRepository<QStateAction> mainRepo,
        IQRecordsExtractor<QStateAction> extractor,
        int episode)
    {
        var evalCurrentTable = $"eval_current_{episode}";
        var evalPrevTable = $"eval_prev_{episode}";
        await mainRepo.CopyTableAsync("q_table", evalCurrentTable);
        await mainRepo.CopyTableAsync("q_table_prev_validation", evalPrevTable);

        var readOnlyOptions = new DuckDbQRepositoryOptions { TableName = "unused", ReadOnly = true };

        var currentRepo = new DuckDbQRepository<QStateAction>(
            dbPath, schema,
            readOnlyOptions with { TableName = evalCurrentTable });
        await currentRepo.CreateTableAsync();

        var prevRepo = new DuckDbQRepository<QStateAction>(
            dbPath, schema,
            readOnlyOptions with { TableName = evalPrevTable });
        await prevRepo.CreateTableAsync();

        return new ValidationContext(
            currentRepo, prevRepo,
            new CachedQRepository(currentRepo),
            new CachedQRepository(prevRepo),
            extractor,
            [evalCurrentTable, evalPrevTable]);
    }

    public IRealtimePlayer CreateCurrentPlayer(Random rng)
        => new Bot(_currentCache, _extractor, 0.0, rng);

    public IRealtimePlayer CreateBaselineOpponent()
        => new SimplePlayer1();

    public IRealtimePlayer CreatePreviousPlayer(Random rng)
        => new Bot(_prevCache, _extractor, 0.0, rng);

    public async ValueTask DisposeAsync()
    {
        await _currentRepo.DisposeAsync();
        await _prevRepo.DisposeAsync();
    }
}
