using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;

namespace Arena.AI.QFolder;

public class QBattleResultsFlushService : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(10);

    private readonly QBattleResultBuffer _buffer;
    private readonly QRecordManager<MinimalQStateAction> _manager;
    private readonly ILogger<QBattleResultsFlushService> _logger;

    public QBattleResultsFlushService(
        QBattleResultBuffer buffer,
        QRecordManager<MinimalQStateAction> repository,
        ILogger<QBattleResultsFlushService> logger)
    {
        _buffer     = buffer;
        _manager = repository;
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

    private async Task FlushAsync()
    {
        var pending = _buffer.DrainAll();

        if (pending.Count == 0)
        {
            _logger.LogDebug("Flush tick: buffer is empty, nothing to write.");
            return;
        }

        _logger.LogInformation("Flush tick: writing {Count} result(s) to DuckDB.", pending.Count);
        await _manager.ProcessBattleResultsAsync(pending);

        return;
    }
}
