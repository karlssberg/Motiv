using System.Threading.Channels;

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
