using Arena.AI.Core.QStorage;

namespace Arena.AI.QFolder;

public interface ITableSchema<TQStateAction> where TQStateAction : QStateAction
{
    /// <summary>Column definitions for CREATE TABLE (state + action columns, no reward).</summary>
    string ColumnDefinitionsSql { get; }

    /// <summary>Comma-separated key column names (state + action) for WHERE, MERGE ON, INDEX.</summary>
    string KeyColumnsCsv { get; }

    /// <summary>All column names including reward, comma-separated, for SELECT/INSERT.</summary>
    string AllColumnsCsv { get; }

    /// <summary>Build a SQL WHERE clause matching all key columns to a state-action.</summary>
    string ToWhereClause(TQStateAction stateAction);

    /// <summary>Build a SQL WHERE clause matching only state columns (excluding action).</summary>
    string ToStateWhereClause(TQStateAction stateAction);

    /// <summary>Build a SQL VALUES tuple string: (col1, col2, ..., reward).</summary>
    string ToValuesTuple(TQStateAction stateAction, double reward);

    /// <summary>Build a SQL VALUES tuple with effective_alpha appended.</summary>
    string ToValuesTupleWithAlpha(TQStateAction stateAction, double reward, double effectiveAlpha);

    /// <summary>Hydrate a state-action + reward from a Dapper dynamic row.</summary>
    (TQStateAction StateAction, double Reward) FromRow(dynamic row);
}
