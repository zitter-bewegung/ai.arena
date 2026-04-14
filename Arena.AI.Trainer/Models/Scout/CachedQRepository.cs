using System.Collections.Concurrent;
using Arena.AI.Core.QStorage;
using Arena.AI.QFolder;

namespace Arena.AI.Trainer.Models.Scout;

public class CachedQRepository : IQRepository<QStateAction>
{
    private static readonly QAction[] AllActions = Enum.GetValues<QAction>();

    private readonly IQRepository<QStateAction> _inner;
    private readonly ConcurrentDictionary<QStateAction, double> _cache = new();

    public CachedQRepository(IQRepository<QStateAction> inner)
    {
        _inner = inner;
    }

    public async Task<double> GetRewardAsync(QStateAction record)
    {
        if (_cache.TryGetValue(record, out var cached))
            return cached;

        if (_inner is DuckDbQRepository<QStateAction> duckRepo)
        {
            var rows = await duckRepo.GetAllForStateAsync(record);
            foreach (var (sa, reward) in rows)
                _cache[sa] = reward;
            foreach (var action in AllActions)
                _cache.TryAdd(record with { Action = action }, 0.0);
            return _cache.TryGetValue(record, out var v) ? v : 0.0;
        }

        var value = await _inner.GetRewardAsync(record);
        _cache[record] = value;
        return value;
    }

    public async Task SaveRecordsAsync(IEnumerable<QRecord<QStateAction>> records)
    {
        await _inner.SaveRecordsAsync(records);
        _cache.Clear();
    }

    public void Invalidate() => _cache.Clear();

    public void WarmUp(Dictionary<QStateAction, double> data)
    {
        _cache.Clear();
        foreach (var (key, value) in data)
            _cache[key] = value;
    }
}
