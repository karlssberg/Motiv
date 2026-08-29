using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Motiv.Serialization.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

/// <summary>
/// The startup schema guard. Two replicas over one store is the documented headline scenario, and
/// both of them call this on the way up.
/// </summary>
public class StoreSchemaTests
{
    [Fact]
    public async Task Should_create_the_three_tables_and_be_a_no_op_the_second_time()
    {
        // Arrange
        var path = TempDatabasePath();
        try
        {
            // Act — the same call every replica makes, twice
            await using (var context = Context(path))
                await StoreSchema.EnsureCreatedAsync(context, NullLogger.Instance);

            await using (var context = Context(path))
                await StoreSchema.EnsureCreatedAsync(context, NullLogger.Instance);

            // Assert — all three tables are readable, which is what the stores need
            await using var check = Context(path);
            (await check.RuleVersions.CountAsync()).ShouldBe(0);
            (await check.Propositions.CountAsync()).ShouldBe(0);
            (await check.StoreGenerations.CountAsync()).ShouldBe(0);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Should_survive_several_instances_creating_the_schema_at_once()
    {
        // Arrange — the crash this exists to stop: instances starting together against one
        // still-empty store, one of them losing the CREATE TABLE race
        var path = TempDatabasePath();
        try
        {
            var starts = Enumerable.Range(0, 8).Select(async _ =>
            {
                await Task.Yield();
                await using var context = Context(path);
                await StoreSchema.EnsureCreatedAsync(context, NullLogger.Instance);
            });

            // Act
            var act = async () => await Task.WhenAll(starts);

            // Assert — every instance came up, and the schema they share is complete
            await act.ShouldNotThrowAsync();

            await using var check = Context(path);
            (await check.RuleVersions.CountAsync()).ShouldBe(0);
            (await check.Propositions.CountAsync()).ShouldBe(0);
            (await check.StoreGenerations.CountAsync()).ShouldBe(0);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Should_surface_the_original_failure_when_the_database_cannot_be_opened()
    {
        // Arrange — an unwritable path stands in for every genuine failure (bad connection string,
        // permission denied): the guard must not turn any of them into a quiet startup
        var path = Path.Combine(Path.GetTempPath(), $"motiv-missing-{Guid.NewGuid():N}", "store.db");
        await using var context = Context(path);

        // Act
        var act = async () => await StoreSchema.EnsureCreatedAsync(context, NullLogger.Instance);

        // Assert — the provider's own exception, not a summary of it
        var thrown = await act.ShouldThrowAsync<SqliteException>();
        thrown.Message.ShouldContain("unable to open database file");
    }

    [Fact]
    public async Task Should_refuse_a_database_whose_motiv_tables_are_missing()
    {
        // Arrange — a database that already holds some other table. EnsureCreated reads that as
        // "already created" and creates nothing, so nothing throws and every Motiv table is absent:
        // checking one table, or trusting the absence of an exception, would boot into this.
        var path = TempDatabasePath();
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE SomebodyElse (Id TEXT PRIMARY KEY)";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = Context(path);

            // Act
            var act = async () => await StoreSchema.EnsureCreatedAsync(context, NullLogger.Instance);

            // Assert — all three are named, so the message says what is actually wrong
            var thrown = await act.ShouldThrowAsync<InvalidOperationException>();
            thrown.Message.ShouldContain("MotivRuleVersion");
            thrown.Message.ShouldContain("MotivProposition");
            thrown.Message.ShouldContain("MotivStoreGeneration");
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static MotivStoreDbContext Context(string path) =>
        new(new DbContextOptionsBuilder<MotivStoreDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options);

    private static string TempDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"motiv-schema-{Guid.NewGuid():N}.db");

    private static void Cleanup(string path)
    {
        // Pooled connections keep a handle on the file, so the delete below fails without this.
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { path, path + "-shm", path + "-wal" })
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
