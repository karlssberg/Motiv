namespace Motiv.Serialization;

/// <summary>The outcome kind of a <see cref="RuleSet.Update"/> or <see cref="RuleSet.Revert"/> call.</summary>
public enum RuleUpdateOutcome
{
    /// <summary>The rule was replaced; <see cref="RuleUpdateResult.Version"/> is the new version.</summary>
    Updated,

    /// <summary>The expected version was stale; <see cref="RuleUpdateResult.Version"/> is the current version.</summary>
    VersionConflict,

    /// <summary>The document failed to bind; <see cref="RuleUpdateResult.Errors"/> holds the errors.</summary>
    Invalid,

    /// <summary>No rule is registered under the given name.</summary>
    NotFound
}

/// <summary>The result of attempting to replace or revert a rule. Expected outcomes are values, not exceptions.</summary>
public sealed class RuleUpdateResult
{
    private RuleUpdateResult(RuleUpdateOutcome outcome, int version, IReadOnlyList<RuleError> errors)
    {
        Outcome = outcome;
        Version = version;
        Errors = errors;
    }

    /// <summary>The outcome kind.</summary>
    public RuleUpdateOutcome Outcome { get; }

    /// <summary>The new version on <see cref="RuleUpdateOutcome.Updated"/>; the current version on <see cref="RuleUpdateOutcome.VersionConflict"/>; otherwise 0.</summary>
    public int Version { get; }

    /// <summary>The binding errors on <see cref="RuleUpdateOutcome.Invalid"/>; otherwise empty.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    /// <summary>The rule was replaced and now has the given version.</summary>
    public static RuleUpdateResult Updated(int newVersion) => new(RuleUpdateOutcome.Updated, newVersion, []);

    /// <summary>The caller's expected version was stale; the rule is at the given version.</summary>
    public static RuleUpdateResult VersionConflict(int currentVersion) => new(RuleUpdateOutcome.VersionConflict, currentVersion, []);

    /// <summary>The document failed structural or semantic binding.</summary>
    public static RuleUpdateResult Invalid(IReadOnlyList<RuleError> errors) => new(RuleUpdateOutcome.Invalid, 0, errors);

    /// <summary>No rule is registered under the requested name.</summary>
    public static RuleUpdateResult NotFound() => new(RuleUpdateOutcome.NotFound, 0, []);
}
