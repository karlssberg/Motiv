using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// A throwaway SQLite database on disk, and a connection factory over it shaped exactly as the sink
/// takes one.
/// </summary>
/// <remarks>
/// On disk rather than in-memory because the point of the sink under test is that a record outlives
/// the connection that wrote it — an in-memory database dies with the last connection, which would
/// make every durability assertion here vacuously true.
/// </remarks>
public sealed class SqliteDecisionFixture : IAsyncDisposable
{
    private readonly string _path;

    private SqliteDecisionFixture(string path)
    {
        _path = path;
        ConnectionString = $"Data Source={path}";
    }

    /// <summary>The connection string, for a second sink over the same file.</summary>
    public string ConnectionString { get; }

    /// <summary>Opens a fresh connection per call, as the sink does.</summary>
    public Func<DbConnection> ConnectionFactory => () => new SqliteConnection(ConnectionString);

    /// <summary>Names a file that does not exist yet; the sink's own bootstrap creates it.</summary>
    public static SqliteDecisionFixture Create() =>
        new(Path.Combine(Path.GetTempPath(), $"motiv-decisions-{Guid.NewGuid():N}.db"));

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Pooled connections keep a handle on the file, so the delete below fails without this.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
        return ValueTask.CompletedTask;
    }
}
