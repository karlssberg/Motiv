namespace Motiv.Serialization;

/// <summary>
/// What an audited evaluation does when the decision queue is full — that is, when the sink is not
/// draining as fast as decisions are being taken.
/// </summary>
/// <remarks>
/// Ticket 09's authoring-store contract deliberately does not govern this. Its reasoning — human-rate
/// publishes, already serialised by one gate — fails entirely on the evaluation hot path, so the
/// posture here is decided on its own terms.
/// </remarks>
public enum DecisionBackpressure
{
    /// <summary>
    /// The evaluation fails: <see cref="DecisionNotLoggedException"/> is thrown and the caller gets no
    /// result. The default, because <c>audited</c> is a claim that the record is load-bearing — if it
    /// were acceptable to lose it, the rule did not need the flag.
    /// </summary>
    FailClosed,

    /// <summary>
    /// The caller waits for capacity. Protects the evidence at the cost of request latency, and on the
    /// synchronous evaluation path that means blocking a thread — which is what choosing this asks
    /// for. The asynchronous rules await instead, and get the same semantics without the thread.
    /// </summary>
    Block,

    /// <summary>
    /// The record is shed and the evaluation proceeds. Protects latency, and never silently: the run
    /// of shed records is written to the log as a <see cref="DecisionGap"/>, so the hole is provable
    /// rather than invisible.
    /// </summary>
    Drop
}
