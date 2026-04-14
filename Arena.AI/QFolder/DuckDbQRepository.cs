using Arena.AI.Core.QStorage;
using Dapper;
using DuckDB.NET.Data;

namespace Arena.AI.QFolder;

public class DuckDbQRepository<TQStateAction> : IQRepository<TQStateAction>, IAsyncDisposable
    where TQStateAction : QStateAction
{
    private readonly DuckDBConnection _connection;
    private readonly ITableSchema<TQStateAction> _schema;
    private readonly DuckDbQRepositoryOptions _options;

    public TextWriter? Log { get; set; }

    public DuckDbQRepository(
        string dbPath,
        ITableSchema<TQStateAction> schema,
        DuckDbQRepositoryOptions options)
    {
        _connection = new DuckDBConnection($"DataSource={dbPath}");
        _schema = schema;
        _options = options;
    }

    public async Task CreateTableAsync()
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync();

        await _connection.ExecuteAsync(
            $"CREATE TABLE IF NOT EXISTS {_options.TableName} ({_schema.ColumnDefinitionsSql}, reward DOUBLE NOT NULL);");

        if (_options.CreateIndex)
        {
            await _connection.ExecuteAsync(
                $"CREATE INDEX IF NOT EXISTS idx_{_options.TableName} ON {_options.TableName} ({_schema.KeyColumnsCsv});");
        }

        if (!_options.ReadOnly)
        {
            var stageCols = _options.UseWeightedAlpha
                ? $"{_schema.ColumnDefinitionsSql}, reward DOUBLE NOT NULL, effective_alpha DOUBLE NOT NULL"
                : $"{_schema.ColumnDefinitionsSql}, reward DOUBLE NOT NULL";
            await _connection.ExecuteAsync(
                $"CREATE TABLE IF NOT EXISTS {_options.StageTableName} ({stageCols});");
        }
    }

    public async Task<double> GetRewardAsync(TQStateAction record)
    {
        return await _connection.QuerySingleOrDefaultAsync<double>(
            $"SELECT reward FROM {_options.TableName} WHERE {_schema.ToWhereClause(record)};");
    }

    public async Task SaveRecordsAsync(IEnumerable<QRecord<TQStateAction>> records)
    {
        if (_options.ReadOnly) return;

        var recordsList = records.ToList();
        var alpha = _options.Alpha;

        var grouped = recordsList
            .GroupBy(r => r.StateAction)
            .Select(g =>
            {
                var n = g.Count();
                var avgReward = g.Average(r => r.Reward);
                var effectiveAlpha = _options.UseWeightedAlpha
                    ? 1.0 - Math.Pow(1.0 - alpha, n)
                    : alpha;
                return (StateAction: g.Key, Reward: avgReward, EffectiveAlpha: effectiveAlpha);
            })
            .ToList();

        var stageColumns = _options.UseWeightedAlpha
            ? $"{_schema.AllColumnsCsv}, effective_alpha"
            : _schema.AllColumnsCsv;

        var values = string.Join(",\n", grouped.Select(r =>
            _options.UseWeightedAlpha
                ? _schema.ToValuesTupleWithAlpha(r.StateAction, r.Reward, r.EffectiveAlpha)
                : _schema.ToValuesTuple(r.StateAction, r.Reward)));

        _connection.Execute($"INSERT INTO {_options.StageTableName} ({stageColumns}) VALUES {values};");

        var mergeOn = string.Join(" AND ",
            _schema.KeyColumnsCsv.Split(',').Select(c => $"t.{c.Trim()} = s.{c.Trim()}"));

        var rewardUpdate = _options.UseWeightedAlpha
            ? "reward = (1 - s.effective_alpha) * t.reward + s.effective_alpha * s.reward"
            : $"reward = (1 - {alpha}) * t.reward + {alpha} * s.reward";

        await _connection.ExecuteAsync($"""
            MERGE INTO {_options.TableName} t
            USING {_options.StageTableName} s
            ON {mergeOn}
            WHEN MATCHED THEN UPDATE SET {rewardUpdate}
            WHEN NOT MATCHED THEN INSERT ({_schema.AllColumnsCsv}) VALUES ({string.Join(", ", _schema.AllColumnsCsv.Split(',').Select(c => $"s.{c.Trim()}"))});
            """);

        await _connection.ExecuteAsync($"TRUNCATE TABLE {_options.StageTableName}");

        var totalRows = await _connection.QuerySingleAsync<long>($"SELECT COUNT(*) FROM {_options.TableName}");
        Log?.WriteLine($"[{DateTime.Now:HH:mm:ss}] flushed {recordsList.Count} reward(s) ({grouped.Count} unique) to {_options.TableName}  (total rows: {totalRows})");
        Log?.Flush();
    }

    public async Task<List<(TQStateAction StateAction, double Reward)>> GetAllForStateAsync(TQStateAction state)
    {
        var rows = await _connection.QueryAsync(
            $"SELECT {_schema.AllColumnsCsv} FROM {_options.TableName} WHERE {_schema.ToStateWhereClause(state)};");

        var result = new List<(TQStateAction, double)>();
        foreach (var row in rows)
        {
            var parsed = _schema.FromRow(row);
            result.Add(parsed);
        }
        return result;
    }

    public async Task<Dictionary<TQStateAction, double>> LoadAllAsync(string? tableOverride = null)
    {
        var table = tableOverride ?? _options.TableName;
        var rows = await _connection.QueryAsync($"SELECT {_schema.AllColumnsCsv} FROM {table};");
        var dict = new Dictionary<TQStateAction, double>();
        foreach (var row in rows)
        {
            var parsed = _schema.FromRow(row);
            dict[parsed.StateAction] = parsed.Reward;
        }
        return dict;
    }

    public async Task CopyTableAsync(string sourceTable, string targetTable)
    {
        await _connection.ExecuteAsync($"CREATE OR REPLACE TABLE {targetTable} AS SELECT * FROM {sourceTable}");
    }

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
