namespace Motiv.Serialization;

/// <summary>
/// Persists the active gate document — the one seam governance needs before spec 2's storage
/// lands. A store is a dumb sink; it validates nothing.
/// </summary>
public interface IGateStore
{
    /// <summary>Loads the currently persisted gate document, or <c>null</c> when none is stored.</summary>
    /// <returns>The stored rule document JSON, or <c>null</c>.</returns>
    string? Load();

    /// <summary>Persists the gate document, replacing whatever was previously stored.</summary>
    /// <param name="documentJson">The rule document JSON to persist, or <c>null</c> to clear it.</param>
    void Save(string? documentJson);
}

/// <summary>The outcome of evaluating a <see cref="ChangeRequest"/> against the active approval gate.</summary>
/// <param name="MayPublish">Whether the change request may be published.</param>
/// <param name="Reason">A one-line summary of why the gate reached this outcome.</param>
/// <param name="Assertions">All contributing assertion strings.</param>
/// <param name="Justification">The full breakdown of contributing causes.</param>
public sealed record GateDecision(
    bool MayPublish, string Reason, IReadOnlyList<string> Assertions, string Justification);

/// <summary>The result of attempting to replace the active gate document.</summary>
public enum GateUpdateOutcome
{
    /// <summary>The document was valid and is now the active gate.</summary>
    Updated,

    /// <summary>The document was structurally or semantically invalid; the gate is unchanged.</summary>
    Invalid,

    /// <summary>
    /// The document is valid but would leave no known role able to satisfy it, locking out all
    /// future changes; the gate is unchanged.
    /// </summary>
    WouldLockOut
}

/// <summary>The result of a <see cref="ApprovalGate.SetGate"/> call.</summary>
/// <param name="Outcome">What happened to the update attempt.</param>
/// <param name="Errors">The validation errors found, or empty when <paramref name="Outcome"/> is <see cref="GateUpdateOutcome.Updated"/>.</param>
/// <param name="PreCheck">The lockout pre-check decision, when <paramref name="Outcome"/> is <see cref="GateUpdateOutcome.WouldLockOut"/>; otherwise <c>null</c>.</param>
public sealed record GateUpdateResult(
    GateUpdateOutcome Outcome, IReadOnlyList<RuleError> Errors, GateDecision? PreCheck);

/// <summary>
/// The may-publish Policy over <see cref="ChangeRequest"/>. Satisfied = may publish; an
/// unsatisfied result blocks and its <see cref="GateDecision.Justification"/> names the unmet
/// conditions. Default: permissive — the only lockout-safe bootstrap; access is still locked by
/// grants, only the ceremony is opt-in.
/// </summary>
public sealed class ApprovalGate
{
    /// <summary>
    /// The reason surfaced when no gate document is configured — every field of the resulting
    /// <see cref="GateDecision"/> carries this same string.
    /// </summary>
    public const string NoGateConfiguredReason = "no approval gate is configured";

    private static readonly RuleSerializer Serializer = new(GateSpecs.CreateRegistry());

    private readonly IGateStore? _store;
    private readonly object _gate = new();

    private string? _documentJson;
    private SpecBase<ChangeRequest, string>? _boundSpec;

    /// <summary>Creates the approval gate, loading and binding any document persisted in <paramref name="store"/>.</summary>
    /// <param name="store">
    /// The store to load the active gate document from and persist future updates to, or
    /// <c>null</c> to run without persistence (always permissive).
    /// </param>
    public ApprovalGate(IGateStore? store = null)
    {
        _store = store;

        var loaded = store?.Load();
        if (loaded is null)
            return;

        var errors = new List<RuleError>();
        var spec = TryBind(loaded, errors);
        if (spec is not null)
        {
            _documentJson = loaded;
            _boundSpec = spec;
        }
    }

    /// <summary>The currently active gate document, or <c>null</c> when the gate is at its permissive default.</summary>
    public string? DocumentJson
    {
        get
        {
            lock (_gate)
                return _documentJson;
        }
    }

    /// <summary>Evaluates a change request against the currently active gate.</summary>
    /// <param name="change">The change request to evaluate.</param>
    /// <returns>
    /// The gate's decision. When no gate is configured this is always <c>MayPublish == true</c>
    /// with <see cref="NoGateConfiguredReason"/> as the reason, assertions, and justification.
    /// </returns>
    public GateDecision Evaluate(ChangeRequest change)
    {
        lock (_gate)
        {
            if (_boundSpec is null)
                return new GateDecision(true, NoGateConfiguredReason, [NoGateConfiguredReason], NoGateConfiguredReason);

            var result = _boundSpec.Evaluate(change);
            return new GateDecision(result.Satisfied, result.Reason, [.. result.Assertions], result.Justification);
        }
    }

    /// <summary>
    /// Validates, binds, and persists a new gate document, replacing the currently active one.
    /// </summary>
    /// <param name="documentJson">The rule document to activate, or <c>null</c> to reset to the permissive default.</param>
    /// <param name="knownRoles">
    /// The roles known to the governance system. Reserved for the lockout pre-check landing in a
    /// later task; not yet consulted.
    /// </param>
    /// <returns>
    /// <see cref="GateUpdateOutcome.Updated"/> with the gate now active, or
    /// <see cref="GateUpdateOutcome.Invalid"/> with the errors found and the gate left unchanged.
    /// </returns>
    public GateUpdateResult SetGate(string? documentJson, IReadOnlyCollection<string> knownRoles)
    {
        if (knownRoles is null) throw new ArgumentNullException(nameof(knownRoles));

        lock (_gate)
        {
            if (documentJson is null)
            {
                _documentJson = null;
                _boundSpec = null;
                _store?.Save(null);
                return new GateUpdateResult(GateUpdateOutcome.Updated, [], null);
            }

            var errors = new List<RuleError>();
            var spec = TryBind(documentJson, errors);
            if (spec is null)
                return new GateUpdateResult(GateUpdateOutcome.Invalid, errors, null);

            _documentJson = documentJson;
            _boundSpec = spec;
            _store?.Save(documentJson);
            return new GateUpdateResult(GateUpdateOutcome.Updated, [], null);
        }
    }

    private static SpecBase<ChangeRequest, string>? TryBind(string documentJson, List<RuleError> errors)
    {
        var validationErrors = Serializer.Validate<ChangeRequest>(documentJson);
        if (validationErrors.Count > 0)
        {
            errors.AddRange(validationErrors);
            return null;
        }

        try
        {
            return Serializer.Deserialize<ChangeRequest>(documentJson);
        }
        catch (RuleSerializationException exception)
        {
            errors.AddRange(exception.Errors);
            return null;
        }
    }
}
