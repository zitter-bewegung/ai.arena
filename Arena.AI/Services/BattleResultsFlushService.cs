namespace Arena.AI.Services;

// Background service that flushes accumulated battle results
// from the in-memory buffer to DuckDB every minute.
// Battle logic is never blocked — the Controller writes to the buffer instantly,
// while this service handles all the heavy database work.
public class BattleResultsFlushService : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);

    private readonly BattleResultBuffer _buffer;
    private readonly DuckDbBattleRepository _repository;
    private readonly ILogger<BattleResultsFlushService> _logger;

    public BattleResultsFlushService(
        BattleResultBuffer buffer,
        DuckDbBattleRepository repository,
        ILogger<BattleResultsFlushService> logger)
    {
        _buffer     = buffer;
        _repository = repository;
        _logger     = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BattleResultsFlushService started. Flush interval: {Interval}.", FlushInterval);

        using var timer = new PeriodicTimer(FlushInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await FlushAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while flushing battle results to DuckDB.");
            }
        }

        _logger.LogInformation("BattleResultsFlushService stopping — performing final flush.");
        await FlushAsync();
    }

    private Task FlushAsync()
    {
        var pending = _buffer.DrainAll();

        if (pending.Count == 0)
        {
            _logger.LogDebug("Flush tick: buffer is empty, nothing to write.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Flush tick: writing {Count} result(s) to DuckDB.", pending.Count);
        _repository.BulkInsert(pending);

        return Task.CompletedTask;
    }
}
