namespace Motiv.Serialization;

/// <summary>
/// Where decision records go. The SDK owns the queue, the batching and the backpressure posture
/// (<see cref="DecisionLog"/>); an implementation of this owns nothing but the writing, and is called
/// on a background writer rather than on the evaluation that produced the records.
/// </summary>
/// <remarks>
/// <para>
/// This is also the "emit, don't store" seam. An adopter who wants their decisions in a SIEM, an
/// outbox or a message bus implements this rather than asking for a feature — and an adopter who needs
/// true zero-loss implements it over a durable queue, because the in-process queue in front of it is a
/// bounded crash-loss window by construction.
/// </para>
/// <para>
/// Implementations are called from one writer loop at a time and need not be thread-safe against
/// themselves, but they must not throw for recoverable conditions: a throwing sink is logged and the
/// loop continues, so a permanently failing sink loses records quietly. Fail fast at construction
/// instead.
/// </para>
/// </remarks>
public interface IDecisionSink
{
    /// <summary>Writes a batch of records, oldest first.</summary>
    /// <param name="records">The batch. Never empty.</param>
    /// <param name="cancellationToken">Cancelled when the log is being disposed.</param>
    Task WriteAsync(IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a gap marker, always immediately ahead of the batch that follows the records it
    /// describes — a marker written after them would misplace the hole.
    /// </summary>
    /// <param name="gap">The run of shed records.</param>
    /// <param name="cancellationToken">Cancelled when the log is being disposed.</param>
    Task WriteGapAsync(DecisionGap gap, CancellationToken cancellationToken);
}
