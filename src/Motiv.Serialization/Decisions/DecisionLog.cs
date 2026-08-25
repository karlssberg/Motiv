using System.Threading.Channels;
using Motiv.Diagnostics;

namespace Motiv.Serialization;

/// <summary>
/// The queue between an audited evaluation and its <see cref="IDecisionSink"/>. Records are handed
/// over synchronously and cheaply; one background writer batches them into the sink.
/// </summary>
/// <remarks>
/// <para>
/// An audited rule on a checkout path must not pay a database write per evaluation, which is why this
/// exists at all. What it buys in latency it owes in durability: everything waiting here is in memory,
/// so the queue is a <strong>bounded crash-loss window</strong> whose size is
/// <see cref="DecisionLogOptions.QueueCapacity"/>. True zero-loss is an <see cref="IDecisionSink"/>
/// over a durable queue — the same seam an adopter uses to emit rather than store.
/// </para>
/// <para>
/// The writer loop never dies. A sink that throws costs its batch and increments
/// <see cref="FailedBatchCount"/>; it does not stop the records behind it from being written, because
/// a log that silently stopped logging is the failure this whole feature exists to prevent.
/// </para>
/// </remarks>
public sealed class DecisionLog : IAsyncDisposable
{
    private readonly IDecisionSink _sink;
    private readonly DecisionLogOptions _options;
    private readonly Channel<DecisionRecord> _queue;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _writer;
    private readonly object _dropLock = new();
    private readonly IDisposable _telemetry;

    private DateTimeOffset _firstDroppedUtc;
    private DateTimeOffset _lastDroppedUtc;
    private long _pendingDropped;
    private long _droppedTotal;
    private long _failedBatchCount;
    private bool _closed;

    /// <summary>Creates a decision log writing to <paramref name="sink"/>.</summary>
    /// <param name="sink">Where records are written.</param>
    /// <param name="options">Queue size, batch size, backpressure posture and capture registry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is null.</exception>
    public DecisionLog(IDecisionSink sink, DecisionLogOptions? options = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _options = options ?? new DecisionLogOptions();

        // FullMode.Wait, not DropWrite: the posture decides what a full queue means, and the channel
        // deciding it for us would make FailClosed unreportable.
        _queue = Channel.CreateBounded<DecisionRecord>(
            new BoundedChannelOptions(_options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });

        _writer = Task.Run(DrainAsync);

        // PII posture is stated once — on the capture registry — and applies to both the durable
        // record and the ephemeral traces. Done here rather than left to the host because a host that
        // forgets is a host quietly exporting into a trace exactly the model data it configured this
        // log not to store, and the forgetting is invisible. Only ever tightens (see
        // ExplanationCeiling), so the order a host configures things in cannot change the outcome,
        // and an adopter who has already chosen something stricter keeps it.
        //
        // Process-wide, and never restored — there is nothing safe to restore it to. A host builds one
        // log at startup, so this is just "configured at startup"; a process that builds several gives
        // the whole process the strictest posture any of them named. That is the right direction to
        // err, but it does mean constructing a log is not a side-effect-free act, which is why this
        // project's tests that construct one are serialized — see RulesTelemetryTestCollection.
        var ceiling = _options.Capture.ExplanationCeiling;
        if (ceiling > MotivTelemetry.ExplanationDetail)
            MotivTelemetry.ExplanationDetail = ceiling;

        // The three decision-log instruments are readings off this object, not events pushed from a
        // call site — motiv.rules.decisions.dropped reads DroppedCount itself, so the counter and the
        // gap markers cannot disagree about how many records were shed. Registering here is what
        // makes them findable; unregistering on disposal keeps a finished log out of the readings.
        _telemetry = MotivRulesTelemetry.DecisionLogs.Add(this);
    }

    /// <summary>How much of an evaluated model each rule's records may keep.</summary>
    public DecisionCaptureRegistry Capture => _options.Capture;

    /// <summary>
    /// How many records have been shed under <see cref="DecisionBackpressure.Drop"/> since this log was
    /// created. Monotonic: taking a gap marker reports a run, it does not forgive it, and the sum of
    /// every gap written equals this.
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _droppedTotal);

    /// <summary>How many batches the sink refused. A rising count is a sink that needs attention.</summary>
    public long FailedBatchCount => Interlocked.Read(ref _failedBatchCount);

    /// <summary>
    /// How many records are waiting for the sink right now — how much of the crash-loss window is
    /// currently occupied.
    /// </summary>
    /// <remarks>
    /// A reading rather than a total, and the one number here that can fall. Depth approaching
    /// <see cref="DecisionLogOptions.QueueCapacity"/> is the warning that
    /// <see cref="DecisionBackpressure"/> is about to start applying — under
    /// <see cref="DecisionBackpressure.FailClosed"/> that means audited evaluations are about to
    /// start throwing, which an operator would much rather see coming than diagnose afterwards.
    /// </remarks>
    public int QueueDepth => _queue.Reader.CanCount ? _queue.Reader.Count : 0;

    /// <summary>
    /// Hands a record to the queue, applying the configured posture when it is full.
    /// </summary>
    /// <param name="record">The record to write.</param>
    /// <exception cref="DecisionNotLoggedException">
    /// The queue is full (or the log is closed) and the posture is
    /// <see cref="DecisionBackpressure.FailClosed"/>.
    /// </exception>
    public void Enqueue(DecisionRecord record)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        if (_queue.Writer.TryWrite(record))
            return;

        // A closed log fails under every posture, not only FailClosed. Capacity is never coming back,
        // so Block would hang forever -- and a disposed log can no longer write the gap marker that
        // makes a drop provable, so dropping here would be exactly the silent loss Drop exists to
        // avoid. Disposal is a lifecycle event, not backpressure.
        if (_closed)
            throw DecisionNotLoggedException.LogClosed(record.RuleName);

        switch (_options.Backpressure)
        {
            case DecisionBackpressure.Block:
                // Deliberately synchronous: the caller chose to protect the evidence at the cost of
                // its own latency, and quietly degrading to Drop would be worse than the wait.
                _queue.Writer.WriteAsync(record).AsTask().GetAwaiter().GetResult();
                return;
            case DecisionBackpressure.Drop:
                RecordDrop();
                return;
            default:
                throw DecisionNotLoggedException.QueueFull(record.RuleName);
        }
    }

    /// <summary>Stops accepting records and waits for the queue to drain into the sink.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_closed)
            return;

        _closed = true;
        _queue.Writer.TryComplete();

        try
        {
            await _writer.ConfigureAwait(false);
        }
        finally
        {
            _shutdown.Cancel();
            _shutdown.Dispose();

            // A drained log has nothing left to report, and a reading of zero from a log nobody is
            // writing to any more would look like health rather than absence.
            _telemetry.Dispose();
        }
    }

    private void RecordDrop()
    {
        lock (_dropLock)
        {
            var now = _options.Clock();
            if (_pendingDropped == 0)
                _firstDroppedUtc = now;
            _lastDroppedUtc = now;
            _pendingDropped++;
            Interlocked.Increment(ref _droppedTotal);
        }
    }

    /// <summary>
    /// Takes the run of drops accumulated so far, resetting the counter. Called by the writer just
    /// before a batch, so the marker lands ahead of the records that followed the hole rather than
    /// behind them — a marker written after them would misplace it.
    /// </summary>
    private DecisionGap? TakePendingGap()
    {
        lock (_dropLock)
        {
            if (_pendingDropped == 0)
                return null;

            var gap = new DecisionGap(_firstDroppedUtc, _lastDroppedUtc, _pendingDropped);
            _pendingDropped = 0;
            return gap;
        }
    }

    private async Task DrainAsync()
    {
        var batch = new List<DecisionRecord>(_options.MaxBatchSize);

        while (await _queue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            batch.Clear();
            while (batch.Count < _options.MaxBatchSize && _queue.Reader.TryRead(out var record))
                batch.Add(record);

            if (batch.Count == 0)
                continue;

            await WriteAsync(batch).ConfigureAwait(false);
        }

        // A run of drops that ended with nothing behind it still has to be reported, or a log that
        // shed its last records would close claiming to be complete.
        if (TakePendingGap() is { } trailing)
            await WriteGapAsync(trailing).ConfigureAwait(false);
    }

    private async Task WriteAsync(List<DecisionRecord> batch)
    {
        if (TakePendingGap() is { } gap)
            await WriteGapAsync(gap).ConfigureAwait(false);

        try
        {
            await _sink.WriteAsync([.. batch], _shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Interlocked.Increment(ref _failedBatchCount);
        }
    }

    private async Task WriteGapAsync(DecisionGap gap)
    {
        try
        {
            await _sink.WriteGapAsync(gap, _shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Interlocked.Increment(ref _failedBatchCount);
        }
    }
}
