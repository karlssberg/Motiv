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
        private int _arrivals;
        public ConcurrentQueue<object> Written { get; } = new();
        public int Failures { get; set; }

        public void Open() => _gate.TrySetResult(true);

        /// <summary>
        /// How many times the writer has handed something over — a batch, or a gap marker. Counted on
        /// entry, <em>before</em> the gate is awaited, so it counts pulls rather than writes — and a
        /// pull is the event that moves <see cref="DecisionLog.QueueDepth"/>. While the gate is closed,
        /// a writer that has arrived here is parked and cannot pull again, so the depth is settled and
        /// stays settled: this is the durable fact a test waits on instead of racing the queue.
        /// </summary>
        public int Arrivals => Volatile.Read(ref _arrivals);

        public async Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
        {
            await ArriveAsync().ConfigureAwait(false);
            if (Failures-- > 0) throw new InvalidOperationException("sink is down");
            foreach (var record in records) Written.Enqueue(record);
        }

        public async Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken)
        {
            await ArriveAsync().ConfigureAwait(false);
            Written.Enqueue(gap);
        }

        /// <summary>
        /// Counts the handover, then blocks on the gate — in that order, and in one place so that
        /// order cannot come adrift for one entry point and not the other. See <see cref="Arrivals"/>.
        /// </summary>
        private Task ArriveAsync()
        {
            Interlocked.Increment(ref _arrivals);
            return _gate.Task;
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
        var reported = harness.For("motiv.rules.decisions.dropped");
        var dropped = log.DroppedCount;

        // Everything is drained before a single assertion runs. An assertion that failed while this
        // sink was still closed would leave the writer parked on the gate forever, and DisposeAsync
        // awaits that writer — turning a one-line test failure into a hung run with no message.
        sink.Open();
        await log.DisposeAsync();

        // Assert — the instrument reads the log's own DroppedCount rather than keeping a second tally
        // beside it, so the counter and the gap markers cannot drift apart.
        dropped.ShouldBeGreaterThan(0, "a capacity-1 queue behind a closed sink must shed");
        reported.ShouldContain(measurement => measurement.Value == dropped);
        sink.Written.OfType<DecisionGap>().Sum(gap => gap.DroppedCount).ShouldBe(dropped);
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

        // Both reads below have to see the same instant, and depth is the one instrument here that
        // can fall — so unlike DroppedCount it must be settled first, not merely sampled twice.
        // Batches are one record and the gate is closed, so the writer takes exactly one record and
        // parks; arriving at the sink *is* the pull, and a parked writer cannot pull again. From that
        // moment the depth is two and stays there. Without this wait the pull lands between Collect()
        // and the read below often enough to redden CI, which is how this failed on net10.0 while
        // passing on the three other frameworks of the same run (#162). The test below pins the
        // hazard down deterministically.
        await WaitUntil(() => sink.Arrivals == 1);

        harness.Collect();

        var reported = harness.For("motiv.rules.decision_queue.depth");
        var depth = log.QueueDepth;

        // Drained before asserting — see the drop test above for why a closed sink and an assertion
        // must never overlap.
        sink.Open();
        await log.DisposeAsync();

        // Assert — a reading, not a total: it is what is at risk right now.
        depth.ShouldBeGreaterThan(0);
        reported.ShouldContain(measurement => measurement.Value == depth);
    }

    [Fact]
    public async Task Should_report_the_depth_the_queue_held_when_it_was_polled_rather_than_when_it_is_read()
    {
        // Arrange — the gate stays closed for the whole act, so the writer parks on the first batch it
        // takes and everything enqueued behind it is frozen. That is what makes the two instants below
        // chosen rather than raced.
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        log.Enqueue(ARecord("warm-up"));
        await WaitUntil(() => sink.Arrivals == 1);

        // Act — poll while the queue is empty, then fill it. A parked writer cannot drain in between,
        // so the gauge is pinned to one instant and the read below to a later, different one.
        harness.Collect();
        var reported = harness.For("motiv.rules.decision_queue.depth");

        log.Enqueue(ARecord("first"));
        log.Enqueue(ARecord("second"));
        log.Enqueue(ARecord("third"));
        var depth = log.QueueDepth;

        sink.Open();
        await log.DisposeAsync();

        // Assert — the gauge holds what the queue held when it was polled, and the queue has moved on.
        // Depth is a sample, not a running total, so a gauge reading and a later QueueDepth are two
        // different observations; requiring them to be equal is a race unless something has pinned the
        // queue still between them. This is what the sibling test above waits for, and what the ticket
        // this test records (#162) tripped over on a contended runner.
        // Stated as "the poll saw zero" rather than the more pointed "no reading equals three". These
        // gauges enumerate every live log in the process, so a negative assertion over one is only as
        // sound as the claim that no other log was collected during that poll — and the last test in
        // this file exists because that claim does not hold. Asserting what this log's poll contained
        // says the same thing without resting on it.
        depth.ShouldBe(3);
        reported.ShouldContain(measurement => measurement.Value == 0);
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
        harness.For("motiv.rules.decision_queue.depth").ShouldContain(measurement => measurement.Value == 0);
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
        harness.For("motiv.rules.decision_batches.failed").ShouldContain(measurement => measurement.Value == 1);
    }

    [Fact]
    public async Task Should_stop_reporting_a_log_that_has_been_disposed()
    {
        // Arrange
        using var harness = new RulesTelemetryHarness();
        var sink = new GatedSink();
        sink.Open();
        var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        harness.Collect();
        var whileAlive = harness.For("motiv.rules.decision_queue.depth").Count;
        whileAlive.ShouldBeGreaterThan(0, "the log must report while it is still running");

        // Act
        await log.DisposeAsync();
        harness.Collect();

        // Assert — a drained log reporting a depth of zero would read as health rather than absence.
        // Expressed as a count because the readings carry no log identity: a host has one decision
        // log, so tagging every measurement to tell several apart would be cardinality for nobody.
        // Fewer rather than exactly one fewer — another test's log elsewhere in this process may have
        // been collected between the two polls, which is the same disappearance for the same reason.
        var whileDisposed = harness.For("motiv.rules.decision_queue.depth").Count - whileAlive;
        whileDisposed.ShouldBeLessThan(whileAlive);
    }
}
