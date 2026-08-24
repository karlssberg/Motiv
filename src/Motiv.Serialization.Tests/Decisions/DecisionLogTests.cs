using System.Collections.Concurrent;

namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// The queue in front of the sink: ordering, batching, the three backpressure postures, and the
/// promise that a drop is never silent.
/// </summary>
public class DecisionLogTests
{
    /// <summary>
    /// A sink that can be held closed, so a full queue is producible rather than hoped for. Pair it
    /// with <c>batchSize: 1</c>: the writer pulls up to a whole batch out of the queue <em>before</em>
    /// it reaches the sink, so a larger batch size drains the queue no matter how tightly it is
    /// bounded, and the postural tests would test nothing.
    /// </summary>
    private sealed class GatedSink : IDecisionSink
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<IReadOnlyList<DecisionRecord>> Batches { get; } = new();
        public ConcurrentQueue<object> Written { get; } = new();
        public int Failures { get; set; }

        public void Open() => _gate.TrySetResult(true);

        public async Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
        {
            await _gate.Task.ConfigureAwait(false);
            if (Failures-- > 0) throw new InvalidOperationException("sink is down");
            Batches.Enqueue(records);
            foreach (var record in records) Written.Enqueue(record);
        }

        public async Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken)
        {
            await _gate.Task.ConfigureAwait(false);
            Written.Enqueue(gap);
        }
    }

    private static DateTimeOffset _now = new(2026, 8, 24, 14, 7, 0, TimeSpan.Zero);

    private static DecisionLogOptions Options(
        DecisionBackpressure backpressure, int capacity = 2, int batchSize = 64) =>
        new()
        {
            Backpressure = backpressure,
            QueueCapacity = capacity,
            MaxBatchSize = batchSize,
            Clock = () => _now
        };

    private static DecisionRecord ARecord(string name) =>
        new(Guid.NewGuid(), "corr", _now, null, name, 1, "build",
            [], null,
            new RuleEvaluationResult<object?>(true, "r", [], [], "j", new ExplanationNode([], [])));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 500 && !condition(); attempt++)
            await Task.Delay(10);
        condition().ShouldBeTrue("the writer never reached the expected state");
    }

    [Fact]
    public async Task Should_write_records_to_the_sink_in_enqueue_order()
    {
        // Arrange
        var sink = new GatedSink();
        sink.Open();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));

        // Act
        log.Enqueue(ARecord("first"));
        log.Enqueue(ARecord("second"));
        log.Enqueue(ARecord("third"));
        await log.DisposeAsync();

        // Assert
        sink.Written.OfType<DecisionRecord>().Select(r => r.RuleName)
            .ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public async Task Should_batch_no_more_than_the_configured_maximum()
    {
        // Arrange — held closed so the records pile up behind one drain
        var sink = new GatedSink();
        await using var log = new DecisionLog(
            sink, Options(DecisionBackpressure.Block, capacity: 64, batchSize: 2));

        // Act
        for (var i = 0; i < 6; i++) log.Enqueue(ARecord($"r{i}"));
        sink.Open();
        await log.DisposeAsync();

        // Assert
        sink.Batches.ShouldAllBe(batch => batch.Count <= 2);
        sink.Written.OfType<DecisionRecord>().Count().ShouldBe(6);
    }

    [Fact]
    public async Task Should_throw_when_the_queue_is_full_and_the_posture_is_fail_closed()
    {
        // Arrange — the sink never drains, so capacity is reachable
        var sink = new GatedSink();
        await using var log = new DecisionLog(
            sink, Options(DecisionBackpressure.FailClosed, capacity: 1, batchSize: 1));

        // Act — fill it, then overflow it
        for (var i = 0; i < 8; i++)
        {
            try { log.Enqueue(ARecord("rule")); }
            catch (DecisionNotLoggedException exception)
            {
                // Assert
                exception.RuleName.ShouldBe("rule");
                exception.Message.ShouldContain("queue is full");
                sink.Open();
                return;
            }
        }

        sink.Open();
        throw new Xunit.Sdk.XunitException("a full queue never refused a record");
    }

    [Fact]
    public async Task Should_wait_for_capacity_when_the_posture_is_block()
    {
        // Arrange
        var sink = new GatedSink();
        await using var log = new DecisionLog(
            sink, Options(DecisionBackpressure.Block, capacity: 1, batchSize: 1));

        // Act — more records than the queue can hold, on a background thread so a genuine block is
        // observable rather than deadlocking the test
        var producer = Task.Run(() =>
        {
            for (var i = 0; i < 8; i++) log.Enqueue(ARecord($"r{i}"));
        });
        await Task.Delay(50);
        var blocked = !producer.IsCompleted;
        sink.Open();
        await producer;
        await log.DisposeAsync();

        // Assert — it waited, and it lost nothing
        blocked.ShouldBeTrue();
        sink.Written.OfType<DecisionRecord>().Count().ShouldBe(8);
    }

    [Fact]
    public async Task Should_shed_records_and_mark_the_gap_when_the_posture_is_drop()
    {
        // Arrange
        var sink = new GatedSink();
        await using var log = new DecisionLog(
            sink, Options(DecisionBackpressure.Drop, capacity: 1, batchSize: 1));

        // Act — overflow while the sink is closed, then let it drain and enqueue one more
        for (var i = 0; i < 8; i++) log.Enqueue(ARecord($"dropped-or-kept-{i}"));
        var dropped = log.DroppedCount;
        dropped.ShouldBeGreaterThan(0, "a capacity-1 queue behind a closed sink must shed");
        sink.Open();
        await WaitUntil(() => sink.Written.OfType<DecisionGap>().Any());
        log.Enqueue(ARecord("after-the-gap"));
        await log.DisposeAsync();

        // Assert — every shed record is accounted for by a marker. The writer may report a run while
        // the producer is still shedding, so the count is the sum rather than a single gap's.
        var written = sink.Written.ToArray();
        var gaps = written.OfType<DecisionGap>().ToArray();
        gaps.ShouldNotBeEmpty();
        gaps.Sum(gap => gap.DroppedCount).ShouldBe(dropped);

        // ...and the hole is marked ahead of what followed it, not behind
        var lastGapIndex = Array.LastIndexOf(written, (object)gaps[^1]);
        var afterIndex = Array.FindIndex(written,
            item => item is DecisionRecord { RuleName: "after-the-gap" });
        lastGapIndex.ShouldBeLessThan(afterIndex);
    }

    [Fact]
    public async Task Should_not_report_a_gap_when_nothing_was_dropped()
    {
        // Arrange
        var sink = new GatedSink();
        sink.Open();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Drop, capacity: 64));

        // Act
        log.Enqueue(ARecord("kept"));
        await log.DisposeAsync();

        // Assert
        sink.Written.OfType<DecisionGap>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_drain_what_is_queued_before_disposal_returns()
    {
        // Arrange
        var sink = new GatedSink();
        await using var log = new DecisionLog(sink, Options(DecisionBackpressure.Block, capacity: 64));
        for (var i = 0; i < 20; i++) log.Enqueue(ARecord($"r{i}"));

        // Act
        sink.Open();
        await log.DisposeAsync();

        // Assert — disposal is the only point at which the crash-loss window is closed on purpose
        sink.Written.OfType<DecisionRecord>().Count().ShouldBe(20);
    }

    [Fact]
    public async Task Should_keep_writing_after_a_sink_throws()
    {
        // Arrange — the first batch fails, the rest must still land
        var sink = new GatedSink { Failures = 1 };
        sink.Open();
        await using var log = new DecisionLog(
            sink, Options(DecisionBackpressure.Block, capacity: 64, batchSize: 1));

        // Act
        log.Enqueue(ARecord("lost"));
        await WaitUntil(() => log.FailedBatchCount > 0);
        log.Enqueue(ARecord("kept"));
        await log.DisposeAsync();

        // Assert — a failing sink must not silently take the writer loop with it
        log.FailedBatchCount.ShouldBe(1);
        sink.Written.OfType<DecisionRecord>().Select(r => r.RuleName).ShouldContain("kept");
    }

    [Fact]
    public async Task Should_refuse_a_record_after_disposal()
    {
        // Arrange
        var sink = new GatedSink();
        sink.Open();
        var log = new DecisionLog(sink, Options(DecisionBackpressure.FailClosed, capacity: 64));

        // Act
        await log.DisposeAsync();

        // Assert — a closed log that quietly accepted records would lose them
        Should.Throw<DecisionNotLoggedException>(() => log.Enqueue(ARecord("late")));
    }
}
