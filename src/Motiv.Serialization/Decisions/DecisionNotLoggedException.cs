namespace Motiv.Serialization;

/// <summary>
/// Thrown when an audited evaluation could not be recorded and no posture could absorb it. The
/// evaluation itself fails, and the caller gets no result: an audited decision that was not logged
/// did not happen.
/// </summary>
public sealed class DecisionNotLoggedException : InvalidOperationException
{
    private DecisionNotLoggedException(string ruleName, string message) : base(message) =>
        RuleName = ruleName;

    /// <summary>The rule whose evaluation could not be recorded.</summary>
    public string RuleName { get; }

    /// <summary>
    /// The queue is full under <see cref="DecisionBackpressure.FailClosed"/> — the sink is not
    /// draining, and the adopter chose the evidence over the request.
    /// </summary>
    /// <param name="ruleName">The rule whose evaluation could not be recorded.</param>
    public static DecisionNotLoggedException QueueFull(string ruleName) =>
        new(ruleName,
            $"Rule '{ruleName}' is audited, but its decision could not be queued for logging: the " +
            "decision queue is full, which means the sink is not draining. The evaluation has been " +
            "failed rather than completed unrecorded. Configure DecisionBackpressure.Block or " +
            "DecisionBackpressure.Drop to change this.");

    /// <summary>
    /// The log has been disposed. Every posture fails here, including
    /// <see cref="DecisionBackpressure.Drop"/>: capacity is not coming back, so Block would hang
    /// forever, and a disposed log cannot write the gap marker that makes a drop provable — so
    /// dropping would be the silent loss Drop exists to avoid.
    /// </summary>
    /// <param name="ruleName">The rule whose evaluation could not be recorded.</param>
    public static DecisionNotLoggedException LogClosed(string ruleName) =>
        new(ruleName,
            $"Rule '{ruleName}' is audited, but the decision log has been disposed, so its decision " +
            "could not be recorded. The evaluation has been failed rather than completed unrecorded. " +
            "Evaluate audited rules before shutting the log down.");
}
