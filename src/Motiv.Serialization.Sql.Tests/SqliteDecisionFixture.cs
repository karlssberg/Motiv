using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// A throwaway SQLite database on disk, a connection factory over it shaped exactly as the sink takes
/// one, and a sink built on both.
/// </summary>
/// <remarks>
/// <para>
/// On disk rather than in-memory because the point of the sink under test is that a record outlives
/// the connection that wrote it — an in-memory database dies with the last connection, which would
/// make every durability assertion here vacuously true.
/// </para>
/// <para>
/// Pooling is off so the file can be deleted at teardown. The alternative,
/// <c>SqliteConnection.ClearAllPools()</c>, is process-global — and xunit runs test classes in
/// parallel, so one fixture's teardown would reach into every other test's connections.
/// </para>
/// </remarks>
public sealed class SqliteDecisionFixture : IAsyncDisposable
{
    private readonly string _path;

    private SqliteDecisionFixture(string path)
    {
        _path = path;
        ConnectionString = $"Data Source={path};Pooling=False";
    }

    /// <summary>The connection string, for a second sink over the same file.</summary>
    public string ConnectionString { get; }

    /// <summary>Opens a fresh connection per call, as the sink does.</summary>
    public Func<DbConnection> ConnectionFactory => () => new SqliteConnection(ConnectionString);

    /// <summary>Names a file that does not exist yet; the sink's own bootstrap creates it.</summary>
    public static SqliteDecisionFixture Create() =>
        new(Path.Combine(Path.GetTempPath(), $"motiv-decisions-{Guid.NewGuid():N}.db"));

    /// <summary>
    /// A sink over this database, with the two required options filled in and everything else at its
    /// default until <paramref name="configure"/> says otherwise.
    /// </summary>
    /// <param name="configure">Overrides the retention window, the purge dials or the clock.</param>
    public SqlDecisionSink Sink(Action<SqlDecisionSinkOptions>? configure = null)
    {
        var options = new SqlDecisionSinkOptions
        {
            Dialect = DecisionSqlDialect.Sqlite,
            Retention = TimeSpan.FromDays(90)
        };

        configure?.Invoke(options);
        return new SqlDecisionSink(ConnectionFactory, options);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        return ValueTask.CompletedTask;
    }
}
