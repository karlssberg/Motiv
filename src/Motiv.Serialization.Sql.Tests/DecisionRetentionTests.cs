using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// The retention window, and the purge that honours it. Version history is kept forever; this is the
/// record that is genuinely unbounded — an audited rule on a hot path is millions of rows — so the
/// window is not a setting the sink can be built without, and the purge is not a job an adopter can
/// forget to register.
/// </summary>
public class DecisionRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Should_purge_records_past_the_window_and_keep_those_inside()
    {
        // Arrange — a thirty-day window, one record on either side of it
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(database, TimeSpan.FromDays(30));
        await sink.WriteAsync(
        [
            Decisions.Record(correlationId: "stale", timestampUtc: Now.AddDays(-31)),
            Decisions.Record(correlationId: "fresh", timestampUtc: Now.AddDays(-29))
        ], CancellationToken.None);

        // Act
        var report = await sink.PurgeAsync();

        // Assert
        report.RecordsPurged.ShouldBe(1);
        report.CutoffUtc.ShouldBe(Now.AddDays(-30));
        (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem()
            .CorrelationId.ShouldBe("fresh");
    }

    [Fact]
    public async Task Should_purge_gaps_on_the_same_window()
    {
        // Arrange — a marker for a hole among records that have themselves aged out would leave the
        // log claiming a gap in a period it no longer covers
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(database, TimeSpan.FromDays(30));
        await sink.WriteGapAsync(new DecisionGap(Now.AddDays(-32), Now.AddDays(-31), 5), CancellationToken.None);
        await sink.WriteGapAsync(new DecisionGap(Now.AddDays(-29), Now.AddDays(-28), 3), CancellationToken.None);

        // Act
        var report = await sink.PurgeAsync();

        // Assert — keyed on the last drop, so a run straddling the cutoff survives until all of it
        // is past the window
        report.GapsPurged.ShouldBe(1);
        (await sink.ReadGapsAsync()).ShouldHaveSingleItem().DroppedCount.ShouldBe(3);
    }

    [Fact]
    public async Task Should_purge_more_than_one_batch_in_a_pass()
    {
        // Arrange — a batch size of one, so clearing three rows demands three statements. The
        // batching exists so a purge after a long outage does not hold one lock for minutes.
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(database, TimeSpan.FromDays(30), purgeBatchSize: 1);
        await sink.WriteAsync(
        [
            Decisions.Record(timestampUtc: Now.AddDays(-40)),
            Decisions.Record(timestampUtc: Now.AddDays(-39)),
            Decisions.Record(timestampUtc: Now.AddDays(-38))
        ], CancellationToken.None);

        // Act
        var report = await sink.PurgeAsync();

        // Assert — one call, every stale row gone, and the count is the total rather than the batch
        report.RecordsPurged.ShouldBe(3);
        (await sink.ReadAsync(new DecisionQuery())).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_leave_a_log_inside_the_window_untouched()
    {
        // Arrange
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(database, TimeSpan.FromDays(30));
        await sink.WriteAsync([Decisions.Record(timestampUtc: Now)], CancellationToken.None);

        // Act
        var report = await sink.PurgeAsync();

        // Assert
        report.RecordsPurged.ShouldBe(0);
        report.GapsPurged.ShouldBe(0);
        (await sink.ReadAsync(new DecisionQuery())).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_count_what_it_has_purged()
    {
        // Arrange — the reading a host surfaces, since the purge has no instrument of its own
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(database, TimeSpan.FromDays(30));
        await sink.WriteAsync([Decisions.Record(timestampUtc: Now.AddDays(-31))], CancellationToken.None);

        // Act
        await sink.PurgeAsync();
        await sink.PurgeAsync();

        // Assert — cumulative across passes, and the second pass found nothing left to take
        sink.PurgedCount.ShouldBe(1);
        sink.LastPurgeUtc.ShouldBe(Now);
        sink.FailedPurgeCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_purge_on_its_own_loop()
    {
        // Arrange — nobody calls PurgeAsync here. The loop is the point: a purge an adopter has to
        // register is a purge an adopter can omit, and an omitted purge is an unbounded table.
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(
            database, TimeSpan.FromDays(30), purgeInterval: TimeSpan.FromMilliseconds(50));
        await sink.WriteAsync([Decisions.Record(timestampUtc: Now.AddDays(-31))], CancellationToken.None);

        // Act
        await WaitUntil(() => sink.PurgedCount > 0);

        // Assert
        (await sink.ReadAsync(new DecisionQuery())).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_keep_looping_after_a_purge_fails()
    {
        // Arrange — an unreachable database. A purge loop that died on the first transient failure
        // would silently stop enforcing the window, which is the failure the window exists to
        // prevent, arriving quietly.
        await using var database = SqliteDecisionFixture.Create();
        var unreachable = true;
        // ReSharper disable once AccessToModifiedClosure — the flip is the point of the test
        await using var sink = new SqlDecisionSink(
            () => unreachable
                ? throw new InvalidOperationException("the decision database is unreachable")
                : database.ConnectionFactory(),
            new SqlDecisionSinkOptions
            {
                Dialect = DecisionSqlDialect.Sqlite,
                Retention = TimeSpan.FromDays(30),
                PurgeInterval = TimeSpan.FromMilliseconds(50),
                Clock = () => Now
            });

        // Act — let it fail at least once, then let it through
        await WaitUntil(() => sink.FailedPurgeCount > 0);
        unreachable = false;

        // Assert — the loop is still running
        await WaitUntil(() => sink.LastPurgeUtc is not null);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
            await Task.Delay(10, timeout.Token);
    }

    private static SqlDecisionSink Sink(
        SqliteDecisionFixture database,
        TimeSpan retention,
        int purgeBatchSize = 5000,
        TimeSpan? purgeInterval = null) =>
        new(database.ConnectionFactory, new SqlDecisionSinkOptions
        {
            Dialect = DecisionSqlDialect.Sqlite,
            Retention = retention,
            PurgeBatchSize = purgeBatchSize,
            PurgeInterval = purgeInterval ?? TimeSpan.FromHours(1),
            Clock = () => Now
        });
}
