namespace Motiv.Serialization;

/// <summary>
/// A hole in the decision log, written where records were shed. The point of
/// <see cref="DecisionBackpressure.Drop"/> is that it protects latency; the point of writing this is
/// that it does not protect latency <em>silently</em>. A missing record is indistinguishable from a
/// decision that was never taken, which is exactly the ambiguity an audit trail exists to remove.
/// </summary>
/// <param name="FirstDroppedUtc">When the first record of this run was shed.</param>
/// <param name="LastDroppedUtc">When the last record of this run was shed.</param>
/// <param name="DroppedCount">How many records the run shed.</param>
public sealed record DecisionGap(
    DateTimeOffset FirstDroppedUtc,
    DateTimeOffset LastDroppedUtc,
    long DroppedCount);
