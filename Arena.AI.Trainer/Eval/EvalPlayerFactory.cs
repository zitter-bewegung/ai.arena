using Arena.AI.Core;
using Arena.AI.Core.RealtimePlayers;

namespace Arena.AI.Trainer.Eval;

public class EvalPlayerFactory : IAsyncDisposable
{
    private readonly EvalPlayerSpec _spec;
    private readonly IEvalPlayerProvider? _provider;

    private EvalPlayerFactory(EvalPlayerSpec spec, IEvalPlayerProvider? provider = null)
    {
        _spec = spec;
        _provider = provider;
    }

    public static async Task<EvalPlayerFactory> CreateAsync(EvalPlayerSpec spec)
    {
        if (spec.IsHardcoded)
            return new EvalPlayerFactory(spec);

        if (!File.Exists(spec.DbPath))
            throw new FileNotFoundException($"DB file not found: {spec.DbPath}");

        IEvalPlayerProvider provider = spec.ModelName switch
        {
            "dwarf" => await Models.Dwarf.EvalPlayerProvider.CreateAsync(spec.DbPath!),
            "scout" => await Models.Scout.EvalPlayerProvider.CreateAsync(spec.DbPath!),
            _ => throw new ArgumentException($"Unknown model: {spec.ModelName}. Available: dwarf, scout")
        };

        return new EvalPlayerFactory(spec, provider);
    }

    public string DisplayName => _spec.DisplayName;

    public IRealtimePlayer CreatePlayer(Random rng)
    {
        if (_spec.IsHardcoded)
        {
            return _spec.PlayerType switch
            {
                "simple" => new SimplePlayer1(),
                _ => throw new ArgumentException($"Unknown hardcoded player: {_spec.PlayerType}")
            };
        }

        return _provider!.CreatePlayer(rng);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider != null)
            await _provider.DisposeAsync();
    }
}
