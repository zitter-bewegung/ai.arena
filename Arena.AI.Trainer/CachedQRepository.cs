using System.Collections.Concurrent;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;

namespace Arena.AI.Trainer;

/// <summary>
/// Thread-safe read-through cache over any IQRepository. On a cache miss,
/// if the inner repo supports IBatchQRepository, all 5 action rewards for the
/// state are fetched in one query and cached together. Cache is cleared
/// automatically after each SaveRecordsAsync (flush) and can be cleared
/// manually via Invalidate().
/// </summary>
public class CachedQRepository : IQRepository<MinimalQStateAction>
{
    private static readonly MinimalQAction[] AllActions = Enum.GetValues<MinimalQAction>();

    private readonly IQRepository<MinimalQStateAction> _inner;
    private readonly ConcurrentDictionary<MinimalQStateAction, double> _cache = new();
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public CachedQRepository(IQRepository<MinimalQStateAction> inner)
    {
        _inner = inner;
    }

    public async Task<double> GetRewardAsync(MinimalQStateAction record)
    {
        if (_cache.TryGetValue(record, out var cached))
            return cached;

        await _dbLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread may have filled it)
            if (_cache.TryGetValue(record, out cached))
                return cached;

            if (_inner is IBatchQRepository batch)
            {
                var rewards = await batch.GetAllRewardsForStateAsync(record);

                foreach (var action in AllActions)
                {
                    var key = record with { Action = action };
                    _cache[key] = rewards.TryGetValue(action, out var r) ? r : 0.0;
                }

                return _cache.TryGetValue(record, out var v) ? v : 0.0;
            }

            var value = await _inner.GetRewardAsync(record);
            _cache[record] = value;
            return value;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task SaveRecordsAsync(IEnumerable<QRecord<MinimalQStateAction>> records)
    {
        await _inner.SaveRecordsAsync(records);
        _cache.Clear();
    }

    /// <summary>Clear the cache manually (e.g. after snapshot table is replaced).</summary>
    public void Invalidate() => _cache.Clear();

    /// <summary>Bulk-load an entire table into the cache. Zero DB reads during battles after this.</summary>
    public void WarmUp(Dictionary<MinimalQStateAction, double> data)
    {
        _cache.Clear();
        foreach (var (key, value) in data)
            _cache[key] = value;
    }
}
