using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Motiv.Serialization.EntityFrameworkCore;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// A throwaway SQLite database on disk, plus a context factory over it. On disk rather than
/// in-memory so the primary key and the transactions under test are the database's own.
/// </summary>
/// <remarks>
/// Pooling is off so the file can be deleted at teardown, matching <c>SqliteDecisionFixture</c> in
/// Motiv.Serialization.Sql.Tests. The alternative, <c>SqliteConnection.ClearAllPools()</c>, is
/// process-global — and xunit runs test classes in parallel, so one fixture's teardown reached into
/// every other live fixture's pool and disposed a connection handle out from under it
/// (<see href="https://github.com/karlssberg/Motiv/issues/219">#219</see>).
/// </remarks>
public sealed class SqliteStoreFixture : IAsyncDisposable
{
    private readonly string _path;

    private SqliteStoreFixture(string path, IDbContextFactory<MotivStoreDbContext> factory)
    {
        _path = path;
        Factory = factory;
    }

    /// <summary>The database file, so a test can assert teardown actually removed it.</summary>
    public string DatabasePath => _path;

    /// <summary>The one connection string every context over this database is built from.</summary>
    private static string ConnectionString(string path) => $"Data Source={path};Pooling=False";

    /// <summary>Opens a fresh context per call, as the stores do.</summary>
    public IDbContextFactory<MotivStoreDbContext> Factory { get; }

    /// <summary>
    /// A second factory over the same database file, wired with the given interceptors. Lets a
    /// test inject a fault (e.g. a <see cref="SaveChangesInterceptor"/> that throws once) into one
    /// context while <see cref="Factory"/> keeps handing out plain ones, without duplicating the
    /// connection-string plumbing above.
    /// </summary>
    public IDbContextFactory<MotivStoreDbContext> FactoryWith(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MotivStoreDbContext>()
            .UseSqlite(ConnectionString(_path))
            .AddInterceptors(interceptors)
            .Options;
        return new TestContextFactory(options);
    }

    /// <summary>Creates the file and the schema.</summary>
    public static async Task<SqliteStoreFixture> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"motiv-store-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<MotivStoreDbContext>()
            .UseSqlite(ConnectionString(path))
            .Options;
        var factory = new TestContextFactory(options);

        await using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();

        return new SqliteStoreFixture(path, factory);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // No pool clear here on purpose: see the remarks above. Nothing holds a handle, so the file
        // is deletable as it stands.
        if (File.Exists(_path))
            File.Delete(_path);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Implements only the synchronous member: <c>CreateDbContextAsync</c> has a default interface
    /// implementation that forwards to it.
    /// </summary>
    private sealed class TestContextFactory(DbContextOptions<MotivStoreDbContext> options)
        : IDbContextFactory<MotivStoreDbContext>
    {
        public MotivStoreDbContext CreateDbContext() => new(options);
    }
}
