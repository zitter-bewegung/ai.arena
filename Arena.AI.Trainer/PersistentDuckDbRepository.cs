using Arena.AI.Core.Models;
using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;
using Dapper;
using DuckDB.NET.Data;
using System.Globalization;

namespace Arena.AI.Trainer;

/// <summary>
/// Trainer-optimized DuckDB repository that keeps a single persistent connection open.
/// Uses the same stage+MERGE persistence logic as the production DuckDbRepository,
/// but avoids the connection-per-call overhead that makes it prohibitively slow
/// in tight training loops.
/// </summary>
public class PersistentDuckDbRepository : IQRepository<MinimalQStateAction>, IBatchQRepository, IAsyncDisposable
{
    private const double Alpha = 0.1;

    private readonly DuckDBConnection _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Optional writer for flush log messages. Set before training starts.</summary>
    public TextWriter? Log { get; set; }

    public PersistentDuckDbRepository(string dbPath)
    {
        _connection = new DuckDBConnection($"DataSource={dbPath}");
    }

    public async Task OpenAsync()
    {
        await _connection.OpenAsync();

        await _connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS minimal_model (
                actor_unit_type        INTEGER NOT NULL,
                actor_health_level     INTEGER NOT NULL,
                number_of_teammates    TINYINT NOT NULL,
                number_of_enemies      TINYINT NOT NULL,
                distance_to_weakest    INTEGER NOT NULL,
                health_of_weakest      INTEGER NOT NULL,
                distance_to_closest    INTEGER NOT NULL,
                health_of_closest      INTEGER NOT NULL,
                distance_average       INTEGER NOT NULL,
                action                 INTEGER NOT NULL,
                reward                 DOUBLE NOT NULL
            );
            """);

        await _connection.ExecuteAsync(
            """
            CREATE INDEX IF NOT EXISTS idx_minimal_model
            ON minimal_model (
                actor_unit_type, actor_health_level, number_of_teammates, number_of_enemies,
                distance_to_weakest, health_of_weakest, distance_to_closest, health_of_closest,
                distance_average, action
            );
            """);

        await _connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS minimal_model_stage (
                actor_unit_type        INTEGER NOT NULL,
                actor_health_level     INTEGER NOT NULL,
                number_of_teammates    TINYINT NOT NULL,
                number_of_enemies      TINYINT NOT NULL,
                distance_to_weakest    INTEGER NOT NULL,
                health_of_weakest      INTEGER NOT NULL,
                distance_to_closest    INTEGER NOT NULL,
                health_of_closest      INTEGER NOT NULL,
                distance_average       INTEGER NOT NULL,
                action                 INTEGER NOT NULL,
                reward                 DOUBLE NOT NULL,
                effective_alpha        DOUBLE NOT NULL
            );
            """);
    }

    public async Task<double> GetRewardAsync(MinimalQStateAction record)
    {
        await _lock.WaitAsync();
        try
        {
            return await _connection.QuerySingleOrDefaultAsync<double>(
                $"""
                SELECT reward
                FROM minimal_model
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

    public async Task SaveRecordsAsync(IEnumerable<QRecord<MinimalQStateAction>> records)
    {
        await _lock.WaitAsync();
        try
        {
            var recordsList = records.ToList();

            // Group by state-action, compute average reward and effective alpha.
            // effective_alpha = 1 - (1-α)^N — equivalent to N sequential alpha updates.
            // N=1 → 0.25, N=5 → 0.76, N=50 → ~1.0
            var grouped = recordsList
                .GroupBy(r => r.StateAction)
                .Select(g =>
                {
                    var n = g.Count();
                    var avgReward = g.Average(r => r.Reward);
                    var effectiveAlpha = 1.0 - Math.Pow(1.0 - Alpha, n);
                    return (StateAction: g.Key, Reward: avgReward, EffectiveAlpha: effectiveAlpha);
                })
                .ToList();

            var sql = string.Join(",\n", grouped.Select(r => $"""
                (
                    {(int)r.StateAction.ActorUnitType},
                    {(int)r.StateAction.ActorHealthLevel},
                    {r.StateAction.NumberOfTeammates},
                    {r.StateAction.NumberOfEnemies},
                    {(int)r.StateAction.DistanceToWeakest},
                    {(int)r.StateAction.HealthOfWeakest},
                    {(int)r.StateAction.DistanceToClosest},
                    {(int)r.StateAction.HealthOfClosest},
                    {(int)r.StateAction.DistanceAverage},
                    {(int)r.StateAction.Action!.Value},
                    {r.Reward.ToString(CultureInfo.InvariantCulture)},
                    {r.EffectiveAlpha.ToString(CultureInfo.InvariantCulture)}
                )
                """));

            _connection.Execute($"""
                INSERT INTO minimal_model_stage (
                    actor_unit_type, actor_health_level, number_of_teammates, number_of_enemies,
                    distance_to_weakest, health_of_weakest, distance_to_closest, health_of_closest,
                    distance_average, action, reward, effective_alpha
                )
                VALUES
                {sql};
                """);

            await _connection.ExecuteAsync("""
                MERGE INTO minimal_model t
                USING minimal_model_stage s
                ON
                    t.actor_unit_type = s.actor_unit_type AND
                    t.actor_health_level = s.actor_health_level AND
                    t.number_of_teammates = s.number_of_teammates AND
                    t.number_of_enemies = s.number_of_enemies AND
                    t.distance_to_weakest = s.distance_to_weakest AND
                    t.health_of_weakest = s.health_of_weakest AND
                    t.distance_to_closest = s.distance_to_closest AND
                    t.health_of_closest = s.health_of_closest AND
                    t.distance_average = s.distance_average AND
                    t.action = s.action

                WHEN MATCHED THEN
                    UPDATE SET
                        reward = (1 - s.effective_alpha) * t.reward + s.effective_alpha * s.reward
                WHEN NOT MATCHED THEN
                    INSERT (
                        actor_unit_type, actor_health_level, number_of_teammates, number_of_enemies,
                        distance_to_weakest, health_of_weakest, distance_to_closest, health_of_closest,
                        distance_average, action, reward
                    )
                    VALUES (
                        s.actor_unit_type, s.actor_health_level, s.number_of_teammates, s.number_of_enemies,
                        s.distance_to_weakest, s.health_of_weakest, s.distance_to_closest, s.health_of_closest,
                        s.distance_average, s.action, s.reward
                    );
                """);

            await _connection.ExecuteAsync("TRUNCATE TABLE minimal_model_stage");

            var totalRows = await _connection.QuerySingleAsync<long>("SELECT COUNT(*) FROM minimal_model");
            Log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] flushed {recordsList.Count} reward(s) ({grouped.Count} unique) to minimal_model  (total rows: {totalRows})");
            Log?.Flush();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Load all rows from a table into a dictionary. Used for cache warm-up.</summary>
    public async Task<Dictionary<MinimalQStateAction, double>> LoadAllAsync(string tableName = "minimal_model")
    {
        var rows = await _connection.QueryAsync(
            $"""
            SELECT
                actor_unit_type, actor_health_level, number_of_teammates, number_of_enemies,
                distance_to_weakest, health_of_weakest, distance_to_closest, health_of_closest,
                distance_average, action, reward
            FROM {tableName}
            """);

        var table = new Dictionary<MinimalQStateAction, double>();
        foreach (var row in rows)
        {
            var sa = new MinimalQStateAction
            {
                ActorUnitType     = (UnitType)Convert.ToInt32(row.actor_unit_type),
                ActorHealthLevel  = (UnitHealthLevel)Convert.ToInt32(row.actor_health_level),
                NumberOfTeammates = Convert.ToByte(row.number_of_teammates),
                NumberOfEnemies   = Convert.ToByte(row.number_of_enemies),
                DistanceToWeakest = (DistanceLevel)Convert.ToInt32(row.distance_to_weakest),
                HealthOfWeakest   = (UnitHealthLevel)Convert.ToInt32(row.health_of_weakest),
                DistanceToClosest = (DistanceLevel)Convert.ToInt32(row.distance_to_closest),
                HealthOfClosest   = (UnitHealthLevel)Convert.ToInt32(row.health_of_closest),
                DistanceAverage   = (DistanceLevel)Convert.ToInt32(row.distance_average),
                Action            = (MinimalQAction)Convert.ToInt32(row.action),
            };
            table[sa] = Convert.ToDouble(row.reward);
        }
        return table;
    }

    public async Task<Dictionary<MinimalQAction, double>> GetAllRewardsForStateAsync(MinimalQStateAction baseState)
    {
        await _lock.WaitAsync();
        try
        {
            var rows = await _connection.QueryAsync(
                $"""
                SELECT action, reward
                FROM minimal_model
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

    /// <summary>Copy one table to another (CREATE OR REPLACE). Used for snapshots and eval copies.</summary>
    public async Task CopyTableAsync(string sourceTable, string targetTable)
    {
        await _connection.ExecuteAsync(
            $"CREATE OR REPLACE TABLE {targetTable} AS SELECT * FROM {sourceTable}");
    }

    /// <summary>Drop a table if it exists. Used for cleaning up eval tables after validation.</summary>
    public async Task DropTableAsync(string tableName)
    {
        await _connection.ExecuteAsync($"DROP TABLE IF EXISTS {tableName}");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
