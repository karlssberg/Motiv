namespace Motiv.Serialization;

/// <summary>One stored rule that was read but not applied, and why.</summary>
/// <param name="Name">The rule name.</param>
/// <param name="Version">The version the store holds, preserved so a repair can address it.</param>
/// <param name="Errors">Why the stored document did not bind.</param>
public sealed record QuarantinedRule(string Name, int Version, IReadOnlyList<RuleError> Errors);

/// <summary>
/// What <see cref="RuleSet.Load"/> found. Quarantine is deliberately not fatal here — a persisted
/// document failing to bind is an operational reality (a redeploy renames a C# spec a stored rule
/// referenced), and refusing to boot would turn a stale row into an outage.
/// </summary>
/// <remarks>
/// It is equally deliberately not <em>silent</em>. Ticket 02 rejected falling back to the compiled
/// default because a quiet revert to unapproved behaviour is indefensible under an approval gate. A
/// quarantined rule therefore stays on its default — a rule must be able to evaluate, and there is
/// nothing else to bind — but says so here, on <see cref="RuleSetEntry.Quarantine"/>, and through
/// <see cref="ThrowIfQuarantined"/> for a host whose policy is to stop.
/// </remarks>
public sealed class RuleLoadReport
{
    internal RuleLoadReport(IReadOnlyList<QuarantinedRule> quarantined, IReadOnlyList<string> orphaned)
    {
        Quarantined = quarantined;
        Orphaned = orphaned;
    }

    /// <summary>Stored rules that were read but did not bind. Empty on a clean load.</summary>
    public IReadOnlyList<QuarantinedRule> Quarantined { get; }

    /// <summary>
    /// Stored names no rule is registered under. Not a fault: the code no longer declares the rule.
    /// The rows are kept — history outlives the code that produced it — and simply not applied.
    /// </summary>
    public IReadOnlyList<string> Orphaned { get; }

    /// <summary>Whether anything was quarantined.</summary>
    public bool HasQuarantine => Quarantined.Count > 0;

    /// <summary>
    /// Stops startup when any stored rule failed to bind. The fail-fast half of the policy the SDK
    /// leaves to the host: call it to refuse a boot on stale rows, or read
    /// <see cref="Quarantined"/> and decide something else.
    /// </summary>
    /// <exception cref="RuleSerializationException">At least one stored rule was quarantined.</exception>
    public void ThrowIfQuarantined()
    {
        if (!HasQuarantine)
            return;

        var errors = Quarantined.SelectMany(rule => rule.Errors).ToArray();
        var names = string.Join(", ", Quarantined.Select(rule => $"'{rule.Name}' (v{rule.Version})"));

        throw new RuleSerializationException(
            $"{Quarantined.Count} stored rule(s) could not be bound and are quarantined: {names}. " +
            "They are running on their compiled defaults, which is not what was published — repair " +
            "or revert them, or drop ThrowIfQuarantined() to boot anyway.",
            errors);
    }
}
