using Arena.AI.Core.Logic;
using System.Collections.Concurrent;

namespace Arena.AI.Services;

// Thread-safe in-memory buffer for battle results.
// The Controller writes here immediately after each battle — no delays.
// The BackgroundService reads from here and flushes to DuckDB.
public class BattleResultBuffer
{
    private readonly ConcurrentQueue<BattleResult> _queue = new();

    public void Enqueue(BattleResult result) => _queue.Enqueue(result);

    public IReadOnlyList<BattleResult> DrainAll()
    {
        var drained = new List<BattleResult>();
        while (_queue.TryDequeue(out var result))
            drained.Add(result);
        return drained;
    }

    public int Count => _queue.Count;
}
