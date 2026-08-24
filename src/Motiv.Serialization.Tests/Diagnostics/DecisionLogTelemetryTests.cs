using System.Collections.Concurrent;

namespace Motiv.Serialization.Tests.Diagnostics;

/// <summary>
/// The three decision-log instruments, and the property that makes them worth having: each is read
/// off the log itself, so a counter cannot disagree with the log about what happened.
/// </summary>
[Collection(RulesTelemetryTestCollection.Name)]
public class DecisionLogTelemetryTests
{
    /// <summary>A sink held closed on demand, so a full queue is producible rather than hoped for.</summary>
    private sealed class GatedSink : IDecisionSink
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<object> Written { get; } = new();
        public int Failures { get; set; }

        public void Open() => _gate.TrySetResult(true);

        public async Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
        {
            await _gate.Task.ConfigureAwait(false);
            if (Failures-- > 0) throw new InvalidOperationException("sink is down");
            foreach (var record in records) Written.Enqueue(record);
        }

        public async Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken)
        {
            await _gate.Task.ConfigureAwait(false);
            Written.Enqueue(gap);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 24, 14, 7, 0, TimeSpan.Zero);

    private static DecisionLogOptions Options(
        DecisionBackpressure backpressure, int capacity = 2, int batchSize = 1) =>
        new()
        {
            Backpressure = backpressure,
            QueueCapacity = capacity,
            MaxBatchSize = batchSize,
            Clock = () => Now
        };

    private static DecisionRecord ARecord(string name) =>
        new(Guid.NewGuid(), "corr", Now, null, name, 1, "build", [], null,
            new RuleEvaluationResult<object?>(true, "r", [], [], "j", new ExplanationNode([], [])));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 500 && !condition(); attempt++)
            await Task.Delay(10);
        condition().ShouldBeTrue("the writer never reached the expected state");
    }

    [Fact]
    public async Task Should_report_shed_records_as_the_same_number_the_gap_markers_account_for()
    {
        // Arrange — held closed, capacity 1, so everything after the first record is shed.
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Drop, capacity: 1));

        // Act
        for (var i = 0; i < 5; i++)
            log.Enqueue(ARecord($"rule-{i}"));

        harness.Collect();

        // Assert — the instrument is the log's own DroppedCount, not a second tally kept beside it,
        // so the counter and the gap markers cannot drift apart.
        var dropped = harness.Single("motiv.rules.decisions.dropped");
        dropped.Value.ShouldBe(log.DroppedCount);
        dropped.Value.ShouldBeGreaterThan(0);

        sink.Open();
        await log.DisposeAsync();

        var gapped = sink.Written.OfType<DecisionGap>().Sum(gap => gap.DroppedCount);
        gapped.ShouldBe((long)dropped.Value);
    }

    [Fact]
    public async Task Should_report_how_much_of_the_crash_loss_window_is_occupied()
    {
        // Arrange — held closed so the records stay queued and the depth is observable at all.
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        // Act
        log.Enqueue(ARecord("first"));
        log.Enqueue(ARecord("second"));
        log.Enqueue(ARecord("third"));
        harness.Collect();

        // Assert — a reading, not a total: it is what is at risk right now.
        harness.Single("motiv.rules.decision_queue.depth").Value.ShouldBe(log.QueueDepth);
        log.QueueDepth.ShouldBeGreaterThan(0);

        sink.Open();
        await log.DisposeAsync();
    }

    [Fact]
    public async Task Should_drain_the_queue_depth_back_to_zero_once_the_sink_accepts()
    {
        // Arrange
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink();
        sink.Open();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        // Act
        log.Enqueue(ARecord("only"));
        await WaitUntil(() => log.QueueDepth == 0);
        harness.Collect();

        // Assert
        harness.Single("motiv.rules.decision_queue.depth").Value.ShouldBe(0);
    }

    [Fact]
    public async Task Should_report_batches_the_sink_refused()
    {
        // Arrange — one failure, then the sink recovers, because the writer loop must not die with it.
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink { Failures = 1 };
        sink.Open();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        // Act
        log.Enqueue(ARecord("refused"));
        await WaitUntil(() => log.FailedBatchCount == 1);
        harness.Collect();

        // Assert
        harness.Single("motiv.rules.decision_batches.failed").Value.ShouldBe(1);
    }

    [Fact]
    public async Task Should_stop_reporting_a_log_that_has_been_disposed()
    {
        // Arrange
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink();
        sink.Open();
        var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        // Act
        await log.DisposeAsync();
        harness.Collect();

        // Assert — a drained log reporting a depth of zero would read as health rather than absence.
        harness.For("motiv.rules.decision_queue.depth").ShouldBeEmpty();
        harness.For("motiv.rules.decisions.dropped").ShouldBeEmpty();
    }
}
