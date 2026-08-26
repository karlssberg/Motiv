using System.Text;

namespace Motiv.Serialization.Sql;

/// <summary>
/// Every statement the sink issues, composed once per dialect.
/// </summary>
/// <remarks>
/// Built eagerly in the constructor and held for the sink's lifetime: the text never varies, and a
/// machine-rate writer should not be assembling strings per batch. The one exception is the read,
/// whose predicates depend on which filters a query carried — an absent filter must not become a
/// parameter nothing ever binds.
/// </remarks>
/// <param name="dialect">The engine's SQL.</param>
internal sealed class DecisionStatements(DecisionSqlDialect dialect)
{
    /// <summary>The dialect these were built for, so the sink converts values the same way.</summary>
    public DecisionSqlDialect Dialect { get; } = dialect;

    /// <summary>Creates both tables and their indexes, if absent. Idempotent by construction.</summary>
    public IReadOnlyList<string> Schema { get; } =
    [
        dialect.CreateTableIfAbsent(DecisionSchema.DecisionTable, string.Join(", ",
            Column(dialect, DecisionSchema.Id, dialect.GuidType, "NOT NULL PRIMARY KEY"),
            Column(dialect, DecisionSchema.CorrelationId, dialect.NameType, "NOT NULL"),
            Column(dialect, DecisionSchema.TimestampUtc, dialect.TimestampType, "NOT NULL"),
            Column(dialect, DecisionSchema.Caller, dialect.NameType, "NULL"),
            Column(dialect, DecisionSchema.RuleName, dialect.NameType, "NOT NULL"),
            Column(dialect, DecisionSchema.RuleVersion, dialect.IntType, "NOT NULL"),
            Column(dialect, DecisionSchema.BuildId, dialect.NameType, "NOT NULL"),
            Column(dialect, DecisionSchema.PropositionsJson, dialect.JsonType, "NOT NULL"),
            Column(dialect, DecisionSchema.InputKind, dialect.IntType, "NULL"),
            Column(dialect, DecisionSchema.InputJson, dialect.JsonType, "NULL"),
            Column(dialect, DecisionSchema.Satisfied, dialect.BoolType, "NOT NULL"),
            Column(dialect, DecisionSchema.OutcomeJson, dialect.JsonType, "NOT NULL"))),

        dialect.CreateTableIfAbsent(DecisionSchema.GapTable, string.Join(", ",
            Column(dialect, DecisionSchema.Id, dialect.GuidType, "NOT NULL PRIMARY KEY"),
            Column(dialect, DecisionSchema.FirstDroppedUtc, dialect.TimestampType, "NOT NULL"),
            Column(dialect, DecisionSchema.LastDroppedUtc, dialect.TimestampType, "NOT NULL"),
            Column(dialect, DecisionSchema.DroppedCount, dialect.LongType, "NOT NULL"))),

        dialect.CreateIndexIfAbsent(
            DecisionSchema.TimestampIndex, DecisionSchema.DecisionTable, DecisionSchema.TimestampUtc),
        dialect.CreateIndexIfAbsent(
            DecisionSchema.CorrelationIndex, DecisionSchema.DecisionTable, DecisionSchema.CorrelationId),
        dialect.CreateIndexIfAbsent(
            DecisionSchema.GapTimestampIndex, DecisionSchema.GapTable, DecisionSchema.LastDroppedUtc)
    ];

    /// <summary>Appends one decision.</summary>
    public string InsertDecision { get; } = Insert(dialect, DecisionSchema.DecisionTable,
        DecisionSchema.Id,
        DecisionSchema.CorrelationId,
        DecisionSchema.TimestampUtc,
        DecisionSchema.Caller,
        DecisionSchema.RuleName,
        DecisionSchema.RuleVersion,
        DecisionSchema.BuildId,
        DecisionSchema.PropositionsJson,
        DecisionSchema.InputKind,
        DecisionSchema.InputJson,
        DecisionSchema.Satisfied,
        DecisionSchema.OutcomeJson);

    /// <summary>Appends one gap marker.</summary>
    public string InsertGap { get; } = Insert(dialect, DecisionSchema.GapTable,
        DecisionSchema.Id,
        DecisionSchema.FirstDroppedUtc,
        DecisionSchema.LastDroppedUtc,
        DecisionSchema.DroppedCount);

    /// <summary>Deletes at most <c>@batch</c> decisions older than <c>@cutoff</c>.</summary>
    public string PurgeDecisions { get; } =
        dialect.PurgeStatement(DecisionSchema.DecisionTable, DecisionSchema.TimestampUtc);

    /// <summary>
    /// Deletes at most <c>@batch</c> gap markers whose run ended before <c>@cutoff</c> — keyed on the
    /// last drop, so a run straddling the cutoff survives until all of it is past the window.
    /// </summary>
    public string PurgeGaps { get; } =
        dialect.PurgeStatement(DecisionSchema.GapTable, DecisionSchema.LastDroppedUtc);

    /// <summary>Reads gap markers, newest first, capped at <c>@limit</c>.</summary>
    public string SelectGaps { get; } =
        $"SELECT {Columns(dialect, DecisionSchema.Id, DecisionSchema.FirstDroppedUtc, DecisionSchema.LastDroppedUtc, DecisionSchema.DroppedCount)} " +
        $"FROM {dialect.Quote(DecisionSchema.GapTable)} " +
        $"ORDER BY {dialect.Quote(DecisionSchema.LastDroppedUtc)} DESC {dialect.LimitClause}";

    /// <summary>Reads decisions matching <paramref name="query"/>, newest first, capped.</summary>
    /// <param name="query">The filters, any of which may be absent.</param>
    /// <returns>The statement, carrying a parameter for each filter the query actually named.</returns>
    public string SelectDecisions(DecisionQuery query)
    {
        var sql = new StringBuilder("SELECT ")
            .Append(Columns(Dialect,
                DecisionSchema.Id,
                DecisionSchema.CorrelationId,
                DecisionSchema.TimestampUtc,
                DecisionSchema.Caller,
                DecisionSchema.RuleName,
                DecisionSchema.RuleVersion,
                DecisionSchema.BuildId,
                DecisionSchema.PropositionsJson,
                DecisionSchema.InputKind,
                DecisionSchema.InputJson,
                DecisionSchema.Satisfied,
                DecisionSchema.OutcomeJson))
            .Append(" FROM ")
            .Append(Dialect.Quote(DecisionSchema.DecisionTable));

        var predicates = new List<string>(4);
        if (query.CorrelationId is not null)
            predicates.Add($"{Dialect.Quote(DecisionSchema.CorrelationId)} = @correlationId");
        if (query.RuleName is not null)
            predicates.Add($"{Dialect.Quote(DecisionSchema.RuleName)} = @ruleName");
        if (query.FromUtc is not null)
            predicates.Add($"{Dialect.Quote(DecisionSchema.TimestampUtc)} >= @fromUtc");
        if (query.ToUtc is not null)
            predicates.Add($"{Dialect.Quote(DecisionSchema.TimestampUtc)} <= @toUtc");

        if (predicates.Count > 0)
            sql.Append(" WHERE ").AppendJoin(" AND ", predicates);

        return sql
            .Append(" ORDER BY ")
            .Append(Dialect.Quote(DecisionSchema.TimestampUtc))
            .Append(" DESC ")
            .Append(Dialect.LimitClause)
            .ToString();
    }

    private static string Column(DecisionSqlDialect dialect, string name, string type, string constraints) =>
        $"{dialect.Quote(name)} {type} {constraints}";

    private static string Columns(DecisionSqlDialect dialect, params string[] names) =>
        string.Join(", ", names.Select(dialect.Quote));

    private static string Insert(DecisionSqlDialect dialect, string table, params string[] columns) =>
        $"INSERT INTO {dialect.Quote(table)} ({Columns(dialect, columns)}) " +
        $"VALUES ({string.Join(", ", columns.Select(column => $"@{column}"))});";
}
