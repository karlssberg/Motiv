using System.Data.Common;
using System.Globalization;

namespace Motiv.Serialization.Sql;

/// <summary>
/// The handful of places SQL engines genuinely disagree, for the decision log's two tables: how an
/// identifier is quoted, what a column type is called, how a statement is made conditional, how a
/// delete is bounded, and how a read is capped.
/// </summary>
/// <remarks>
/// <para>
/// A seam rather than a provider reference. <see cref="SqlDecisionSink"/> is written against
/// <see cref="DbConnection"/> and takes the connection from the adopter, so the package can honour
/// ticket 16's "may target a different database or engine entirely" instead of merely claiming it.
/// A dialect is what an engine needs beyond ADO.NET, and an adopter with a fourth engine derives one.
/// </para>
/// <para>
/// Parameters are spelled <c>@name</c> throughout, which SQLite, Npgsql and SqlClient all accept — so
/// the one thing that <em>looks</em> most provider-specific turns out not to be, and no dialect member
/// exists for it.
/// </para>
/// </remarks>
public abstract class DecisionSqlDialect
{
    /// <summary>SQLite — the zero-config default, and what <c>docker compose up</c> gets.</summary>
    public static DecisionSqlDialect Sqlite { get; } = new SqliteDecisionSqlDialect();

    /// <summary>PostgreSQL.</summary>
    public static DecisionSqlDialect PostgreSql { get; } = new PostgreSqlDecisionSqlDialect();

    /// <summary>Microsoft SQL Server.</summary>
    public static DecisionSqlDialect SqlServer { get; } = new SqlServerDecisionSqlDialect();

    /// <summary>Wraps an identifier so a reserved word or a mixed-case name survives.</summary>
    /// <param name="identifier">The unquoted table, column or index name.</param>
    /// <returns>The identifier as this engine spells it.</returns>
    public abstract string Quote(string identifier);

    /// <summary>The column type a <see cref="Guid"/> is stored in.</summary>
    protected internal abstract string GuidType { get; }

    /// <summary>The column type a <see cref="DateTimeOffset"/> is stored in.</summary>
    protected internal abstract string TimestampType { get; }

    /// <summary>A bounded text type, for the columns an index key may cover.</summary>
    protected internal abstract string NameType { get; }

    /// <summary>An unbounded text type, for the JSON payloads.</summary>
    protected internal abstract string JsonType { get; }

    /// <summary>The 32-bit integer type.</summary>
    protected internal abstract string IntType { get; }

    /// <summary>The 64-bit integer type.</summary>
    protected internal abstract string LongType { get; }

    /// <summary>The boolean type.</summary>
    protected internal abstract string BoolType { get; }

    /// <summary>
    /// A <c>CREATE TABLE</c> that does nothing when the table is already there — the sink bootstraps
    /// rather than migrates, so a replica restarting against a live table must not fail here.
    /// </summary>
    /// <param name="table">The unquoted table name.</param>
    /// <param name="columns">The column list, already quoted and typed.</param>
    /// <returns>The statement.</returns>
    protected internal abstract string CreateTableIfAbsent(string table, string columns);

    /// <summary>A <c>CREATE INDEX</c> that does nothing when the index is already there.</summary>
    /// <param name="index">The unquoted index name.</param>
    /// <param name="table">The unquoted table name.</param>
    /// <param name="column">The unquoted column name.</param>
    /// <returns>The statement.</returns>
    protected internal abstract string CreateIndexIfAbsent(string index, string table, string column);

    /// <summary>
    /// A <c>DELETE</c> of at most <c>@batch</c> rows older than <c>@cutoff</c>. Bounded because an
    /// unbounded delete after a long outage holds one lock for minutes.
    /// </summary>
    /// <param name="table">The unquoted table name.</param>
    /// <param name="timestampColumn">The unquoted column the cutoff applies to.</param>
    /// <returns>The statement.</returns>
    protected internal abstract string PurgeStatement(string table, string timestampColumn);

    /// <summary>The clause capping a read at <c>@limit</c> rows, appended after <c>ORDER BY</c>.</summary>
    protected internal abstract string LimitClause { get; }

    /// <summary>Converts an identity for a parameter.</summary>
    /// <param name="value">The identity.</param>
    /// <returns>What this engine's provider binds.</returns>
    protected internal virtual object ToParameter(Guid value) => value;

    /// <summary>Converts an instant for a parameter, normalised to UTC.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>What this engine's provider binds.</returns>
    protected internal virtual object ToParameter(DateTimeOffset value) => value.ToUniversalTime();

    /// <summary>Converts a flag for a parameter.</summary>
    /// <param name="value">The flag.</param>
    /// <returns>What this engine's provider binds.</returns>
    protected internal virtual object ToParameter(bool value) => value;

    /// <summary>Reads an identity back.</summary>
    /// <param name="reader">The open reader.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The identity.</returns>
    protected internal virtual Guid ReadGuid(DbDataReader reader, int ordinal) => reader.GetGuid(ordinal);

    /// <summary>Reads an instant back.</summary>
    /// <param name="reader">The open reader.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The instant.</returns>
    protected internal virtual DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal) =>
        reader.GetFieldValue<DateTimeOffset>(ordinal);

    /// <summary>Reads a flag back.</summary>
    /// <param name="reader">The open reader.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The flag.</returns>
    protected internal virtual bool ReadBoolean(DbDataReader reader, int ordinal) => reader.GetBoolean(ordinal);
}

/// <summary>
/// SQLite, which has no dedicated identity, instant or boolean type — so all three are stored as the
/// text or integer they would otherwise be coerced to, spelled explicitly here rather than left to a
/// provider's default mapping.
/// </summary>
/// <remarks>
/// Instants are written as round-trip UTC strings, which sort lexicographically precisely because they
/// are normalised — the <c>&lt; @cutoff</c> the purge depends on is a string comparison, and a mixture
/// of offsets would make it quietly wrong rather than loudly broken.
/// </remarks>
internal sealed class SqliteDecisionSqlDialect : QuotedDecisionSqlDialect
{
    protected internal override string GuidType => "TEXT";
    protected internal override string TimestampType => "TEXT";
    protected internal override string NameType => "TEXT";
    protected internal override string JsonType => "TEXT";
    protected internal override string IntType => "INTEGER";
    protected internal override string LongType => "INTEGER";
    protected internal override string BoolType => "INTEGER";

    protected internal override object ToParameter(Guid value) => value.ToString();

    protected internal override object ToParameter(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    protected internal override object ToParameter(bool value) => value ? 1 : 0;

    protected internal override Guid ReadGuid(DbDataReader reader, int ordinal) =>
        Guid.Parse(reader.GetString(ordinal));

    protected internal override DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(
            reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    protected internal override bool ReadBoolean(DbDataReader reader, int ordinal) =>
        reader.GetInt64(ordinal) != 0;
}

/// <summary>PostgreSQL, whose native types Npgsql already binds from the CLR ones.</summary>
internal sealed class PostgreSqlDecisionSqlDialect : QuotedDecisionSqlDialect
{
    protected internal override string GuidType => "uuid";
    protected internal override string TimestampType => "timestamptz";
    protected internal override string NameType => "varchar(256)";
    protected internal override string JsonType => "text";
    protected internal override string IntType => "integer";
    protected internal override string LongType => "bigint";
    protected internal override string BoolType => "boolean";
}

/// <summary>
/// SQL Server, the one engine here with no <c>IF NOT EXISTS</c> on <c>CREATE TABLE</c> and no
/// <c>LIMIT</c> — so it guards its DDL with a catalog lookup and caps its reads with
/// <c>OFFSET … FETCH</c>.
/// </summary>
internal sealed class SqlServerDecisionSqlDialect : DecisionSqlDialect
{
    protected internal override string GuidType => "uniqueidentifier";
    protected internal override string TimestampType => "datetimeoffset";

    // Bounded rather than nvarchar(max) because CorrelationId is an index key, and SQL Server will
    // not put a max-length column in one.
    protected internal override string NameType => "nvarchar(256)";

    protected internal override string JsonType => "nvarchar(max)";
    protected internal override string IntType => "int";
    protected internal override string LongType => "bigint";
    protected internal override string BoolType => "bit";

    protected internal override string LimitClause => "OFFSET 0 ROWS FETCH NEXT @limit ROWS ONLY";

    public override string Quote(string identifier) => $"[{identifier}]";

    protected internal override string CreateTableIfAbsent(string table, string columns) =>
        $"IF OBJECT_ID(N'{Quote(table)}', N'U') IS NULL CREATE TABLE {Quote(table)} ({columns});";

    protected internal override string CreateIndexIfAbsent(string index, string table, string column) =>
        $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' " +
        $"AND object_id = OBJECT_ID(N'{Quote(table)}')) " +
        $"CREATE INDEX {Quote(index)} ON {Quote(table)} ({Quote(column)});";

    protected internal override string PurgeStatement(string table, string timestampColumn) =>
        $"DELETE TOP (@batch) FROM {Quote(table)} WHERE {Quote(timestampColumn)} < @cutoff;";
}

/// <summary>
/// What SQLite and PostgreSQL share: double-quoted identifiers, <c>IF NOT EXISTS</c> on both kinds of
/// <c>CREATE</c>, <c>LIMIT</c>, and a delete bounded by a keyed sub-select.
/// </summary>
internal abstract class QuotedDecisionSqlDialect : DecisionSqlDialect
{
    protected internal override string LimitClause => "LIMIT @limit";

    public override string Quote(string identifier) => $"\"{identifier}\"";

    protected internal override string CreateTableIfAbsent(string table, string columns) =>
        $"CREATE TABLE IF NOT EXISTS {Quote(table)} ({columns});";

    protected internal override string CreateIndexIfAbsent(string index, string table, string column) =>
        $"CREATE INDEX IF NOT EXISTS {Quote(index)} ON {Quote(table)} ({Quote(column)});";

    protected internal override string PurgeStatement(string table, string timestampColumn) =>
        $"DELETE FROM {Quote(table)} WHERE {Quote(DecisionSchema.Id)} IN (" +
        $"SELECT {Quote(DecisionSchema.Id)} FROM {Quote(table)} " +
        $"WHERE {Quote(timestampColumn)} < @cutoff LIMIT @batch);";
}
