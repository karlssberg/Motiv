namespace Motiv.Serialization;

/// <summary>
/// Thrown when an audited evaluation could not be recorded and the configured posture is
/// <see cref="DecisionBackpressure.FailClosed"/>. The evaluation itself fails, and the caller gets no
/// result: an audited decision that was not logged did not happen.
/// </summary>
public sealed class DecisionNotLoggedException : InvalidOperationException
{
    /// <summary>Creates the exception for a named rule.</summary>
    /// <param name="ruleName">The rule whose evaluation could not be recorded.</param>
    public DecisionNotLoggedException(string ruleName)
        : base($"Rule '{ruleName}' is audited, but its decision could not be queued for logging: " +
               "the decision queue is full, which means the sink is not draining. The evaluation has " +
               "been failed rather than completed unrecorded. Configure DecisionBackpressure.Block or " +
               "DecisionBackpressure.Drop to change this.") =>
        RuleName = ruleName;

    /// <summary>The rule whose evaluation could not be recorded.</summary>
    public string RuleName { get; }
}
