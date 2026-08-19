using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization.EntityFrameworkCore;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// A throwaway SQLite database on disk, plus a context factory over it. On disk rather than
/// in-memory so the primary key and the transactions under test are the database's own.
/// </summary>
public sealed class SqliteStoreFixture : IAsyncDisposable
{
    private readonly string _path;

    private SqliteStoreFixture(string path, IDbContextFactory<MotivStoreDbContext> factory)
    {
        _path = path;
        Factory = factory;
    }

    /// <summary>Opens a fresh context per call, as the stores do.</summary>
    public IDbContextFactory<MotivStoreDbContext> Factory { get; }

    /// <summary>Creates the file and the schema.</summary>
    public static async Task<SqliteStoreFixture> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"motiv-store-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<MotivStoreDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var factory = new TestContextFactory(options);

        await using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();

        return new SqliteStoreFixture(path, factory);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Pooled connections keep a handle on the file, so the delete below fails without this.
        SqliteConnection.ClearAllPools();
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
