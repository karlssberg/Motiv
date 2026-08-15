namespace Motiv.Serialization;

/// <summary>
/// A rule change that has bound successfully and is waiting to go live. Mirrors
/// <see cref="IRebindCommit"/>: preparing everything before committing anything is what makes a
/// publish all-or-nothing, and here it is also what makes room for the store write — the one step
/// between "this binds" and "this is live" that can still fail.
/// </summary>
internal interface IRulePublication
{
    /// <summary>The version this publication will carry once committed.</summary>
    int Version { get; }

    /// <summary>The document it will carry, or null for a return to the compiled default.</summary>
    string? DocumentJson { get; }

    /// <summary>
    /// Makes the change live. Cannot fail, and must be called under the scope lock, inside the same
    /// <see cref="BindingScope.Mutate(Action{ScopeGenerationBuilder})"/> that re-tracks the rule's
    /// graph edges — so the binding and the edges that describe it publish in one swap rather than
    /// two. <paramref name="builder"/> is threaded through for that reason, not because a rule's
    /// state lives in it yet — it still lives in a field on the rule itself, swapped by a plain
    /// <see cref="Volatile.Write{T}"/>.
    /// </summary>
    void ApplyTo(ScopeGenerationBuilder builder);
}

/// <summary>
/// The outcome of preparing a rule change: a publication ready to commit, or the reason there is none.
/// The same four outcomes <see cref="RuleUpdateResult"/> reports, one stage earlier.
/// </summary>
internal sealed class RulePrepareResult
{
    private RulePrepareResult(
        RuleUpdateOutcome outcome, int version, IReadOnlyList<RuleError> errors, IRulePublication? publication)
    {
        Outcome = outcome;
        Version = version;
        Errors = errors;
        Publication = publication;
    }

    /// <summary>The outcome kind. <see cref="RuleUpdateOutcome.Updated"/> means "prepared", not "live".</summary>
    public RuleUpdateOutcome Outcome { get; }

    /// <summary>The prepared version, or the current version on a conflict; otherwise 0.</summary>
    public int Version { get; }

    /// <summary>The binding errors on <see cref="RuleUpdateOutcome.Invalid"/>; otherwise empty.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    /// <summary>The publication to commit, or null on any outcome but a successful prepare.</summary>
    public IRulePublication? Publication { get; }

    public static RulePrepareResult Prepared(IRulePublication publication) =>
        new(RuleUpdateOutcome.Updated, publication.Version, [], publication);

    public static RulePrepareResult VersionConflict(int currentVersion) =>
        new(RuleUpdateOutcome.VersionConflict, currentVersion, [], null);

    public static RulePrepareResult Invalid(IReadOnlyList<RuleError> errors) =>
        new(RuleUpdateOutcome.Invalid, 0, errors, null);

    public static RulePrepareResult NotFound() =>
        new(RuleUpdateOutcome.NotFound, 0, [], null);

    /// <summary>
    /// The caller-facing result for a prepare that did not produce a publication. Calling this on a
    /// successful prepare would report a publish that has not happened, so it refuses.
    /// </summary>
    public RuleUpdateResult ToFailureResult() =>
        Outcome switch
        {
            RuleUpdateOutcome.VersionConflict => RuleUpdateResult.VersionConflict(Version),
            RuleUpdateOutcome.Invalid => RuleUpdateResult.Invalid(Errors),
            RuleUpdateOutcome.NotFound => RuleUpdateResult.NotFound(),
            _ => throw new InvalidOperationException(
                "A prepared publication has not been committed yet, so there is no result to report. " +
                "Commit it and report RuleUpdateResult.Updated, or call this only on a failed prepare.")
        };
}
