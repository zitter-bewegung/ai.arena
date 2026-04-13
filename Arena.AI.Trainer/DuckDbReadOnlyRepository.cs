using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;
using Dapper;
using DuckDB.NET.Data;

namespace Arena.AI.Trainer;

/// <summary>
/// Read-only DuckDB repository that queries a specific table by name.
/// Used for reading snapshot/eval tables from background validation threads,
/// each instance opens its own connection for thread safety.
/// </summary>
public class DuckDbReadOnlyRepository : IQRepository<MinimalQStateAction>, IBatchQRepository, IAsyncDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly string _tableName;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DuckDbReadOnlyRepository(string dbPath, string tableName)
    {
        _connection = new DuckDBConnection($"DataSource={dbPath}");
        _tableName = tableName;
    }

    public async Task OpenAsync() => await _connection.OpenAsync();

    public async Task<double> GetRewardAsync(MinimalQStateAction record)
    {
        await _lock.WaitAsync();
        try
        {
            return await _connection.QuerySingleOrDefaultAsync<double>(
                $"""
                SELECT reward
                FROM {_tableName}
                WHERE actor_unit_type = {(int)record.ActorUnitType}
                  AND actor_health_level = {(int)record.ActorHealthLevel}
                  AND number_of_teammates = {record.NumberOfTeammates}
                  AND number_of_enemies = {record.NumberOfEnemies}
                  AND distance_to_weakest = {(int)record.DistanceToWeakest}
                  AND health_of_weakest = {(int)record.HealthOfWeakest}
                  AND distance_to_closest = {(int)record.DistanceToClosest}
                  AND health_of_closest = {(int)record.HealthOfClosest}
                  AND distance_average = {(int)record.DistanceAverage}
                  AND action = {(int)record.Action!.Value};
                """);
        }
        finally { _lock.Release(); }
    }

    public async Task<Dictionary<MinimalQAction, double>> GetAllRewardsForStateAsync(MinimalQStateAction baseState)
    {
        await _lock.WaitAsync();
        try
        {
            var rows = await _connection.QueryAsync(
                $"""
                SELECT action, reward
                FROM {_tableName}
                WHERE actor_unit_type = {(int)baseState.ActorUnitType}
                  AND actor_health_level = {(int)baseState.ActorHealthLevel}
                  AND number_of_teammates = {baseState.NumberOfTeammates}
                  AND number_of_enemies = {baseState.NumberOfEnemies}
                  AND distance_to_weakest = {(int)baseState.DistanceToWeakest}
                  AND health_of_weakest = {(int)baseState.HealthOfWeakest}
                  AND distance_to_closest = {(int)baseState.DistanceToClosest}
                  AND health_of_closest = {(int)baseState.HealthOfClosest}
                  AND distance_average = {(int)baseState.DistanceAverage}
                """);

            var result = new Dictionary<MinimalQAction, double>();
            foreach (var row in rows)
                result[(MinimalQAction)Convert.ToInt32(row.action)] = Convert.ToDouble(row.reward);
            return result;
        }
        finally { _lock.Release(); }
    }

    public Task SaveRecordsAsync(IEnumerable<QRecord<MinimalQStateAction>> records)
        => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
