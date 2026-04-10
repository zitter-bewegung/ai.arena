using Arena.AI.Core.QStorage;
using Arena.AI.Core.QStorage.QRecords.MinimalQRecords;
using Dapper;
using DuckDB.NET.Data;
using System.Globalization;

namespace Arena.AI.QFolder;

public class DuckDbRepository : IQRepository<MinimalQStateAction>
{
    private const double alpha = 0.1;

    private readonly string _connectionString;

    public DuckDbRepository(IConfiguration configuration)
    {
        var dbPath = configuration["DuckDb:Path"] ?? "battles.db";
        _connectionString = $"DataSource={dbPath}";
    }

    public async Task CreateTableAsync()
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
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
        await connection.CloseAsync();
    } 

    public async Task<double> GetRewardAsync(MinimalQStateAction record)
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        var result = await connection.QuerySingleOrDefaultAsync<double>(
            $@"""
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
              AND action = {(record.Action)};
            """);

        await connection.CloseAsync();

        return result;
    }

    public async Task SaveRecordsAsync(IEnumerable<QRecord<MinimalQStateAction>> records)
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        var sql = string.Join(",\n", records.Select(r => $"""
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
                    {(int)r.StateAction.Action.Value},
                    {r.Reward.ToString(CultureInfo.InvariantCulture)}
                )
                """));

        var fullSql = $"""
                    INSERT INTO minimal_model_stage (
                        actor_unit_type,
                        actor_health_level,
                        number_of_teammates,
                        number_of_enemies,
                        distance_to_weakest,
                        health_of_weakest,
                        distance_to_closest,
                        health_of_closest,
                        distance_average,
                        action,
                        reward
                    )
                    VALUES
                    {sql};
                    """;

        connection.Execute(fullSql);

        /*await connection.ExecuteAsync(
            """
            INSERT INTO minimal_model_stage (actor_unit_type, actor_health_level, number_of_teammates, number_of_enemies, distance_to_weakest, health_of_weakest, distance_to_closest, health_of_closest, distance_average, action, reward)
            VALUES (@ActorUnitType, @ActorHealthLevel, @NumberOfTeammates, @NumberOfEnemies, @DistanceToWeakest, @HealthOfWeakest, @DistanceToClosest, @HealthOfClosest, @DistanceAverage, @Action, @Reward);
            """,
            records.Select(record => new
            {
                ActorUnitType     = record.StateAction.ActorUnitType,
                ActorHealthLevel  = record.StateAction.ActorHealthLevel,
                NumberOfTeammates = record.StateAction.NumberOfTeammates,
                NumberOfEnemies   = record.StateAction.NumberOfEnemies,
                DistanceToWeakest = record.StateAction.DistanceToWeakest,
                HealthOfWeakest   = record.StateAction.HealthOfWeakest,
                DistanceToClosest = record.StateAction.DistanceToClosest,
                HealthOfClosest   = record.StateAction.HealthOfClosest,
                DistanceAverage   = record.StateAction.DistanceAverage,
                Action            = record.StateAction.Action,
                Reward            = record.Reward
            }));*/

        await connection.ExecuteAsync($"""
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
                    reward = (1 - {alpha.ToString(CultureInfo.InvariantCulture)}) * t.reward + {alpha.ToString(CultureInfo.InvariantCulture)} * s.reward,

            WHEN NOT MATCHED THEN
                INSERT (
                    actor_unit_type,
                    actor_health_level,
                    number_of_teammates,
                    number_of_enemies,
                    distance_to_weakest,
                    health_of_weakest,
                    distance_to_closest,
                    health_of_closest,
                    distance_average,
                    action,
                    reward
                )
                VALUES (
                    s.actor_unit_type,
                    s.actor_health_level,
                    s.number_of_teammates,
                    s.number_of_enemies,
                    s.distance_to_weakest,
                    s.health_of_weakest,
                    s.distance_to_closest,
                    s.health_of_closest,
                    s.distance_average,
                    s.action,
                    s.reward
                );
            """, new {alpha});

        await connection.ExecuteAsync("TRUNCATE TABLE minimal_model_stage");

        await connection.CloseAsync();
    }
}
