using Shouldly;
using Xunit;

namespace Motiv.Serialization.Sql.Tests;

/// <summary>
/// The whole path, end to end: <see cref="DecisionLog"/>'s queue draining into a real database, and
/// the records still being there when the object that wrote them is gone. This is the difference
/// between this sink and <c>InMemoryDecisionSink</c> stated as a test.
/// </summary>
public class DurableDecisionLogTests
{
    [Fact]
    public async Task Should_outlive_the_log_that_wrote_it()
    {
        // Arrange
        await using var database = SqliteDecisionFixture.Create();

        // Act — write through the queue, then dispose everything that holds state in memory
        await using (var sink = Sink(database))
        await using (var log = new DecisionLog(sink))
        {
            log.Enqueue(Decisions.Record(correlationId: "trace-1"));
            log.Enqueue(Decisions.Record(correlationId: "trace-2"));
        }

        // Assert — a second sink over the same file, standing in for a restarted process
        await using var reader = Sink(database);
        var read = await reader.ReadAsync(new DecisionQuery());
        read.Select(record => record.CorrelationId).ShouldBe(["trace-2", "trace-1"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_persist_a_gap_marker_from_a_dropped_run()
    {
        // Arrange — a queue of one, the Drop posture, and a sink held shut until every enqueue has
        // happened, so the run of shed records is deterministic rather than a race
        await using var database = SqliteDecisionFixture.Create();
        await using var sink = Sink(database);
        var released = new TaskCompletionSource();
        var gate = new GatedSink(sink, released.Task);

        await using (var log = new DecisionLog(gate, new DecisionLogOptions
                     {
                         QueueCapacity = 1,
                         Backpressure = DecisionBackpressure.Drop
                     }))
        {
            // Fill the queue, then shed the rest
            for (var index = 0; index < 5; index++)
                log.Enqueue(Decisions.Record(correlationId: $"trace-{index}"));

            log.DroppedCount.ShouldBeGreaterThan(0);

            // Act — let the writer through and drain on disposal
            released.SetResult();
        }

        // Assert — the hole is provable in the durable log, not only in the process that made it
        var gaps = await sink.ReadGapsAsync();
        gaps.Sum(gap => gap.DroppedCount).ShouldBe(5 - (await sink.ReadAsync(new DecisionQuery())).Count);
    }

    private static SqlDecisionSink Sink(SqliteDecisionFixture database) =>
        new(database.ConnectionFactory, new SqlDecisionSinkOptions
        {
            Dialect = DecisionSqlDialect.Sqlite,
            Retention = TimeSpan.FromDays(90)
        });

    /// <summary>Holds the writer loop shut until a test says otherwise, then forwards everything.</summary>
    private sealed class GatedSink(IDecisionSink inner, Task released) : IDecisionSink
    {
        public async Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
        {
            await released;
            await inner.WriteAsync(records, cancellationToken);
        }

        public async Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken)
        {
            await released;
            await inner.WriteGapAsync(gap, cancellationToken);
        }
    }
}
