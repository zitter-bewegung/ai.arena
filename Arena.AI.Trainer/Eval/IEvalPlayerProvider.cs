using Arena.AI.Core;

namespace Arena.AI.Trainer.Eval;

public interface IEvalPlayerProvider : IAsyncDisposable
{
    IRealtimePlayer CreatePlayer(Random rng);
}
