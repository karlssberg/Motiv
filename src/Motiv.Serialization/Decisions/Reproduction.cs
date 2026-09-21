namespace Motiv.Serialization;

/// <summary>Why a reproduction is not an exact re-run of the logged decision.</summary>
public enum FidelityReason
{
    /// <summary>The rule log lacks the pinned version, or this host has no such rule; nothing replays.</summary>
    RuleVersionMissing,

    /// <summary>The decision was made on another build; the compiled specs a document names may differ.</summary>
    BuildMismatch,

    /// <summary>A pinned proposition version is not in the proposition log; its live head was bound instead, when there was one.</summary>
    PropositionVersionMissing,

    /// <summary>A pinned document no longer binds, or the rule's own did not. Nothing replays: a rule that resolved the name through today's head would not be reproducing the decision.</summary>
    PropositionBindFailed,

    /// <summary>The capture was a projection; fields the rule reads may be absent from the replayed model.</summary>
    ModelRedacted,

    /// <summary>No model could be produced: nothing was captured, no resolver is registered, or the subject is gone.</summary>
    ModelUnresolved,

    /// <summary>The replay decided differently from the log. Loud on purpose: it is the finding a reproduction exists to surface.</summary>
    OutcomeDiverged,
}

/// <summary>One way the reproduction fell short of exact, with what exactly slipped.</summary>
public sealed record FidelityNote(FidelityReason Reason, string Detail)
{
    /// <inheritdoc />
    public override string ToString() => $"{Reason}: {Detail}";
}

/// <summary>The verdict on a reproduction: exact when nothing had to be noted.</summary>
public sealed record ReproductionFidelity(IReadOnlyList<FidelityNote> Notes)
{
    /// <summary>True when every anchor was honoured and the replay agreed with the log.</summary>
    public bool IsExact => Notes.Count == 0;
}

/// <summary>How the replayed model was arrived at.</summary>
public enum ReproducedModelKind
{
    /// <summary>The whole model, as captured.</summary>
    Whole,

    /// <summary>A projection, as captured; rehydrated as far as it goes.</summary>
    Redacted,

    /// <summary>A reference-only capture, resolved through the registered resolver.</summary>
    Resolved,

    /// <summary>A reference-only capture that could not be resolved; only the key is known.</summary>
    Reference,

    /// <summary>Nothing was captured, or the rule is unknown so nothing could be rehydrated.</summary>
    Absent,
}

/// <summary>The model a reproduction ran, or the most it could recover.</summary>
/// <param name="Value">The model, or null when none could be produced.</param>
/// <param name="Key">The reference key, for a reference-only capture.</param>
public sealed record ReproducedModel(ReproducedModelKind Kind, object? Value, string? Key);

/// <summary>
/// A logged decision run again: the rule and every referenced proposition at the version that
/// decided, the model as far as capture and a resolver allow, the replay's result, and the verdict
/// on how exact all of that was.
/// </summary>
/// <param name="Rule">The pinned rule version row, or null when the log lacks it.</param>
/// <param name="Propositions">The pinned proposition rows that were bound, heads standing in where a pin was missing.</param>
/// <param name="Replayed">The re-evaluation, or null when nothing could run.</param>
public sealed record Reproduction(
    DecisionRecord Decision,
    StoredRuleVersion? Rule,
    IReadOnlyList<StoredPropositionVersion> Propositions,
    ReproducedModel Model,
    RuleEvaluationResult<object?>? Replayed,
    ReproductionFidelity Fidelity);

/// <summary>The log holds no decision with the requested id; retention may have purged it.</summary>
public sealed class DecisionNotFoundException(Guid id)
    : InvalidOperationException($"No decision '{id}' is in the log; retention may have purged it.")
{
    /// <summary>The id that was asked for.</summary>
    public Guid Id { get; } = id;
}
