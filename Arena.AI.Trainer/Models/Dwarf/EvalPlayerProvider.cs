using Arena.AI.Core;
using Arena.AI.QFolder;
using Arena.AI.Trainer.Eval;

namespace Arena.AI.Trainer.Models.Dwarf;

public class EvalPlayerProvider : IEvalPlayerProvider
{
    private readonly DuckDbQRepository<QStateAction> _repo;
    private readonly CachedQRepository _cache;
    private readonly RecordExtractor _extractor;

    private EvalPlayerProvider(
        DuckDbQRepository<QStateAction> repo,
        CachedQRepository cache,
        RecordExtractor extractor)
    {
        _repo = repo;
        _cache = cache;
        _extractor = extractor;
    }

    public static async Task<EvalPlayerProvider> CreateAsync(string dbPath)
    {
        var repo = new DuckDbQRepository<QStateAction>(
            dbPath,
            new TableSchema(),
            new DuckDbQRepositoryOptions { TableName = "q_table", ReadOnly = true });
        await repo.CreateTableAsync();

        return new EvalPlayerProvider(repo, new CachedQRepository(repo), new RecordExtractor());
    }

    public IRealtimePlayer CreatePlayer(Random rng)
        => new Bot(_cache, _extractor, 0.0, rng);

    public async ValueTask DisposeAsync() => await _repo.DisposeAsync();
}
