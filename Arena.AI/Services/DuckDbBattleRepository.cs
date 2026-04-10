using Arena.AI.Core.Logic;
using DuckDB.NET.Data;

namespace Arena.AI.Services;

// Repository for persisting battle results to DuckDB.
public class DuckDbBattleRepository
{
    private readonly string _connectionString;
    private readonly ILogger<DuckDbBattleRepository> _logger;

    public DuckDbBattleRepository(IConfiguration configuration, ILogger<DuckDbBattleRepository> logger)
    {
        _logger = logger;
        var dbPath = configuration["DuckDb:Path"] ?? "battles.db";
        _connectionString = $"DataSource={dbPath}";
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        using var connection = new DuckDBConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS battle_results (
                battle_id     VARCHAR PRIMARY KEY,
                winner        VARCHAR NOT NULL,
                actions_count INTEGER NOT NULL,
                recorded_at   VARCHAR NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
        _logger.LogInformation("DuckDB table 'battle_results' is ready.");
    }

    // Writes a batch of results in a single block inside a transaction.
    public void BulkInsert(IReadOnlyList<BattleResult> results)
    {
        if (results.Count == 0) return;

        using var connection = new DuckDBConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var r in results)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT OR IGNORE INTO battle_results (battle_id, winner, actions_count, recorded_at)
                    VALUES (?, ?, ?, ?);
                    """;

                var p1 = cmd.CreateParameter();
                p1.Value = r.BattleId;

                var p2 = cmd.CreateParameter();
                p2.Value = r.Winner;

                var p3 = cmd.CreateParameter();
                p3.Value = r.Actions?.Count ?? 0;

                var p4 = cmd.CreateParameter();
                p4.Value = DateTime.UtcNow.ToString("o");

                cmd.Parameters.Add(p1);
                cmd.Parameters.Add(p2);
                cmd.Parameters.Add(p3);
                cmd.Parameters.Add(p4);

                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
            _logger.LogInformation("Flushed {Count} battle result(s) to DuckDB.", results.Count);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}