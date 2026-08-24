namespace Motiv.Serialization;

/// <summary>How the decision log queues, batches and sheds — and what it is allowed to keep of a model.</summary>
public sealed class DecisionLogOptions
{
    private int _queueCapacity = 1024;
    private int _maxBatchSize = 64;

    /// <summary>
    /// How many records may wait for the sink before <see cref="Backpressure"/> applies. Defaults to
    /// 1,024.
    /// </summary>
    /// <remarks>
    /// This is the size of the crash-loss window, not a throughput dial: everything queued here is in
    /// memory, and a process that dies takes it with it. Keep it shallow enough that the window is
    /// small and deep enough that an ordinary sink hiccup does not reach the posture. An adopter who
    /// needs no window at all writes an <see cref="IDecisionSink"/> over a durable queue.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int QueueCapacity
    {
        get => _queueCapacity;
        set => _queueCapacity = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "QueueCapacity must be at least 1.");
    }

    /// <summary>The largest batch handed to the sink in one call. Defaults to 64.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxBatchSize
    {
        get => _maxBatchSize;
        set => _maxBatchSize = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "MaxBatchSize must be at least 1.");
    }

    /// <summary>
    /// What an audited evaluation does when the queue is full. Defaults to
    /// <see cref="DecisionBackpressure.FailClosed"/>.
    /// </summary>
    public DecisionBackpressure Backpressure { get; set; } = DecisionBackpressure.FailClosed;

    /// <summary>
    /// How much of the evaluated model each rule's records may keep. A rule marked <c>audited</c> over
    /// a model type with no posture registered here <strong>will not bind</strong> — see
    /// <see cref="DecisionCaptureRegistry"/>.
    /// </summary>
    public DecisionCaptureRegistry Capture { get; } = new();

    /// <summary>The clock records are stamped from. Injected so tests need not wait for real time.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;
}
