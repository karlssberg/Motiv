namespace Motiv.Serialization;

/// <summary>A dependent that the attempted edit would have stopped binding.</summary>
/// <param name="Name">The dependent's name.</param>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
/// <param name="Errors">Why it would no longer bind.</param>
public sealed record BrokenDependent(string Name, string Kind, IReadOnlyList<RuleError> Errors);

/// <summary>
/// A node that references a proposition and is rebound when it is republished. Distinct from
/// <see cref="BrokenDependent"/>: listing the blast radius is not reporting a failure, and reusing
/// the failure type with an empty error list would blur the two.
/// </summary>
/// <param name="Name">The dependent's name.</param>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
public sealed record PropositionDependent(string Name, string Kind);

/// <summary>The outcome kind of a <see cref="PropositionSet"/> mutation.</summary>
public enum PropositionUpdateOutcome
{
    /// <summary>A new proposition was authored.</summary>
    Created,

    /// <summary>An existing proposition's document was replaced.</summary>
    Updated,

    /// <summary>The authored document was withdrawn — reverting to a compiled spec, or removing it outright.</summary>
    Removed,

    /// <summary>The expected version was stale.</summary>
    VersionConflict,

    /// <summary>The document, or a dependent of it, failed to bind.</summary>
    Invalid,

    /// <summary>No proposition — authored or compiled — is known under the name.</summary>
    NotFound,

    /// <summary>A proposition is already authored under the name.</summary>
    NameTaken,

    /// <summary>Removal would leave referrers dangling.</summary>
    Referenced
}

/// <summary>
/// The result of attempting to author, replace, or withdraw a proposition. Expected outcomes are
/// values rather than exceptions, mirroring <see cref="RuleUpdateResult"/>.
/// </summary>
public sealed class PropositionUpdateResult
{
    private PropositionUpdateResult(
        PropositionUpdateOutcome outcome,
        int version,
        IReadOnlyList<RuleError> errors,
        IReadOnlyList<BrokenDependent> brokenDependents,
        IReadOnlyList<string> referrers)
    {
        Outcome = outcome;
        Version = version;
        Errors = errors;
        BrokenDependents = brokenDependents;
        Referrers = referrers;
    }

    /// <summary>The outcome kind.</summary>
    public PropositionUpdateOutcome Outcome { get; }

    /// <summary>The new version on success; the current version on <see cref="PropositionUpdateOutcome.VersionConflict"/>; otherwise 0.</summary>
    public int Version { get; }

    /// <summary>Errors in the submitted document itself; empty when the document was fine but a dependent broke.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    /// <summary>Dependents the edit would have broken; empty unless that is why it was rejected.</summary>
    public IReadOnlyList<BrokenDependent> BrokenDependents { get; }

    /// <summary>The names blocking a removal on <see cref="PropositionUpdateOutcome.Referenced"/>; otherwise empty.</summary>
    public IReadOnlyList<string> Referrers { get; }

    /// <summary>A new proposition was authored at version 1.</summary>
    public static PropositionUpdateResult Created(int version) =>
        new(PropositionUpdateOutcome.Created, version, [], [], []);

    /// <summary>The document was replaced and the proposition now has the given version.</summary>
    public static PropositionUpdateResult Updated(int version) =>
        new(PropositionUpdateOutcome.Updated, version, [], [], []);

    /// <summary>The authored document was withdrawn.</summary>
    public static PropositionUpdateResult Removed() =>
        new(PropositionUpdateOutcome.Removed, 0, [], [], []);

    /// <summary>The caller's expected version was stale.</summary>
    public static PropositionUpdateResult VersionConflict(int currentVersion) =>
        new(PropositionUpdateOutcome.VersionConflict, currentVersion, [], [], []);

    /// <summary>The submitted document failed structural or semantic binding.</summary>
    public static PropositionUpdateResult Invalid(IReadOnlyList<RuleError> errors) =>
        new(PropositionUpdateOutcome.Invalid, 0, errors, [], []);

    /// <summary>The submitted document bound, but one or more dependents would not.</summary>
    public static PropositionUpdateResult BreaksDependents(IReadOnlyList<BrokenDependent> broken) =>
        new(PropositionUpdateOutcome.Invalid, 0, [], broken, []);

    /// <summary>Nothing is known under the requested name.</summary>
    public static PropositionUpdateResult NotFound() =>
        new(PropositionUpdateOutcome.NotFound, 0, [], [], []);

    /// <summary>A proposition is already authored under the requested name.</summary>
    public static PropositionUpdateResult NameTaken() =>
        new(PropositionUpdateOutcome.NameTaken, 0, [], [], []);

    /// <summary>Removal is blocked by the given referrers.</summary>
    public static PropositionUpdateResult Referenced(IReadOnlyList<string> referrers) =>
        new(PropositionUpdateOutcome.Referenced, 0, [], [], referrers);
}
