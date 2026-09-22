using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// What disposal does, and — more importantly — what it does not do.
/// </summary>
public class SqlDecisionSinkLifecycleTests
{
    [Fact]
    public async Task Should_stop_purging_when_disposed_but_keep_writing()
    {
        // Arrange — a container disposes singletons in reverse creation order, so a sink created
        // before the DecisionLog that drains into it is torn down first. If disposal closed the write
        // path, that ordering would silently swallow the drain the log's own disposal exists to
        // perform.
        //
        // Nothing calls EnsureSchemaAsync first, on purpose: the zero-config path bootstraps on the
        // first write, so a disposal that took the bootstrap down with it would fail exactly here
        // and nowhere else.
        await using var database = SqliteDecisionFixture.Create();
        var sink = database.Sink(options =>
        {
            options.Retention = TimeSpan.FromDays(30);
            options.PurgeInterval = TimeSpan.FromMilliseconds(20);
            options.Clock = () => new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        });

        // Act
        await sink.DisposeAsync();
        await sink.WriteAsync([Decisions.Record()], CancellationToken.None);

        // Assert — the record landed, schema and all, and the loop that would have taken it is stopped
        (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();
        var purgesAtDisposal = sink.PurgedCount;
        await Task.Delay(100);
        sink.PurgedCount.ShouldBe(purgesAtDisposal);
    }

    [Fact]
    public async Task Should_dispose_a_connection_that_would_not_open()
    {
        // Arrange — a database that fails at Open rather than at the factory, which is how a real
        // one goes down. DecisionRetentionTests covers a factory that throws; that path never
        // reaches OpenAsync, so the connection it creates and abandons on a failed open was
        // exercised by nothing — and the purge loop retries every interval, so a connection leaked
        // per attempt against an unreachable database is a leak that compounds.
        // The path is built rather than written out so it is unopenable on every platform: SQLite
        // does not create intermediate directories, and these do not exist.
        var missing = Path.Combine(
            Path.GetTempPath(), $"motiv-absent-{Guid.NewGuid():N}", "nested", "decisions.db");

        var unopenable = new SqlDecisionSink(
            () => new SqliteConnection($"Data Source={missing}"),
            new SqlDecisionSinkOptions
            {
                Dialect = DecisionSqlDialect.Sqlite,
                Retention = TimeSpan.FromDays(30)
            });
        await using var sink = unopenable;

        // Act
        var act = async () => await sink.WriteAsync([Decisions.Record()], CancellationToken.None);

        // Assert — the failure surfaces rather than being swallowed, which is what lets
        // DecisionLog count the refused batch and carry on
        await Should.ThrowAsync<SqliteException>(act);
    }

    [Fact]
    public async Task Should_tolerate_being_disposed_twice()
    {
        // Arrange
        await using var database = SqliteDecisionFixture.Create();
        var sink = database.Sink(options => options.Retention = TimeSpan.FromDays(30));

        // Act
        await sink.DisposeAsync();
        var act = async () => await sink.DisposeAsync();

        // Assert
        await Should.NotThrowAsync(act);
    }
}
