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

    /// <summary>
    /// Built once and shared by every <see cref="ApprovalGate"/> constructed with the public
    /// constructor: the built-in gate registry (<see cref="GateSpecs.CreateRegistry"/>) is
    /// immutable and stateless, so there is no benefit to rebuilding it per instance. Tests that
    /// need a different registry (e.g. one carrying a throwaway async entry) use the internal
    /// constructor overload instead, which takes its own registry rather than sharing this one.
    /// </summary>
    private static readonly SpecRegistry DefaultRegistry = GateSpecs.CreateRegistry();

    private readonly IGateStore? _store;
    private readonly SpecRegistry _registry;
    private readonly RuleSerializerOptions _parserOptions = new();
    private readonly RuleSerializer _serializer;
    private readonly object _gate = new();

    private string? _documentJson;
    private SpecBase<ChangeRequest, string>? _boundSpec;

    /// <summary>Creates the approval gate, loading and binding any document persisted in <paramref name="store"/>.</summary>
    /// <param name="store">
    /// The store to load the active gate document from and persist future updates to, or
    /// <c>null</c> to run without persistence (always permissive).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="store"/> holds a document that fails to validate or bind. A store is a dumb
    /// sink — it cannot have rejected a bad write — so a stored-but-invalid document means the
    /// governance store was tampered with or corrupted. The gate fails closed and loud rather than
    /// silently falling back to permissive; recovery (fixing or clearing the stored document, or
    /// break-glass at the infrastructure layer) is an operational act, not something this
    /// constructor can safely paper over.
    /// </exception>
    public ApprovalGate(IGateStore? store = null) : this(store, DefaultRegistry)
    {
    }

    /// <summary>
    /// Creates the approval gate against a caller-supplied registry, rather than the shared
    /// built-in <see cref="GateSpecs"/> catalogue. Exists so tests can prove the synchronous-only
    /// guard against a registry carrying an async entry the built-in catalogue never has — the
    /// public constructor has no way to express that, since it always binds the built-in registry.
    /// </summary>
    /// <param name="store">
    /// The store to load the active gate document from and persist future updates to, or
    /// <c>null</c> to run without persistence (always permissive).
    /// </param>
    /// <param name="registry">The registry gate documents resolve spec references against.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="store"/> holds a document that fails to validate or bind. See the public
    /// constructor's remarks.
    /// </exception>
    internal ApprovalGate(IGateStore? store, SpecRegistry registry)
    {
        _store = store;
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _serializer = new RuleSerializer(_registry, _parserOptions);

        var loaded = store?.Load();
        if (loaded is null)
            return;

        var errors = new List<RuleError>();
        var spec = TryBind(loaded, errors);
        if (spec is null)
        {
            var details = string.Join("; ", errors);
            throw new InvalidOperationException(
                $"The gate document loaded from the configured IGateStore is invalid and cannot " +
                $"be bound; the gate cannot start. Fix or clear the stored document to recover. " +
                $"Errors: {details}");
        }

        _documentJson = loaded;
        _boundSpec = spec;
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
                // Persist before swapping: if Save throws, the in-memory gate must stay exactly
                // as it was rather than diverge from what's now (or still) on disk.
                _store?.Save(null);
                _documentJson = null;
                _boundSpec = null;
                return new GateUpdateResult(GateUpdateOutcome.Updated, [], null);
            }

            var errors = new List<RuleError>();
            var spec = TryBind(documentJson, errors);
            if (spec is null)
                return new GateUpdateResult(GateUpdateOutcome.Invalid, errors, null);

            // Persist before swapping — same rationale as the null-reset path above.
            _store?.Save(documentJson);
            _documentJson = documentJson;
            _boundSpec = spec;
            return new GateUpdateResult(GateUpdateOutcome.Updated, [], null);
        }
    }

    /// <summary>
    /// Binds a candidate gate document, folding in the gate's own synchronous-only guard: the guard
    /// runs on a structural parse taken before <see cref="RuleSerializer.Validate{TModel}"/> ever
    /// reaches the binder, so a document referencing an async registry entry reports
    /// <see cref="RuleErrorCode.GateMustBeSynchronous"/> — not the binder's generic
    /// <see cref="RuleErrorCode.AsyncSpecInSyncLoad"/>, which would otherwise fire first and name a
    /// mistake this isn't: the gate document is a perfectly legal synchronous load target in
    /// general, just not of an async entry the gate itself must evaluate.
    /// </summary>
    private SpecBase<ChangeRequest, string>? TryBind(string documentJson, List<RuleError> errors)
    {
        var structuralErrors = new List<RuleError>();
        var document = new RuleDocumentParser(_parserOptions).Parse(documentJson, structuralErrors);
        if (structuralErrors.Count > 0)
        {
            errors.AddRange(structuralErrors);
            return null;
        }

        if (FindAsyncReference(document) is { } asyncSpecName)
        {
            errors.Add(new RuleError(
                "$.rule", RuleErrorCode.GateMustBeSynchronous,
                $"the gate document references '{asyncSpecName}', which is registered as an " +
                "asynchronous spec; the approval gate evaluates change requests synchronously and " +
                "cannot bind to an async entry"));
            return null;
        }

        var validationErrors = _serializer.Validate<ChangeRequest>(documentJson);
        if (validationErrors.Count > 0)
        {
            errors.AddRange(validationErrors);
            return null;
        }

        try
        {
            return _serializer.Deserialize<ChangeRequest>(documentJson);
        }
        catch (RuleSerializationException exception)
        {
            errors.AddRange(exception.Errors);
            return null;
        }
    }

    /// <summary>
    /// The first referenced spec name that resolves in this gate's registry to an async entry, or
    /// <c>null</c> when every reference is synchronous (or unresolved — an unknown-spec reference is
    /// reported separately by the ordinary validate/bind pipeline this guard runs ahead of).
    /// </summary>
    private string? FindAsyncReference(RuleDocument? document)
    {
        if (document?.Root is null)
            return null;

        foreach (var name in DocumentReferences.From(document))
            if (_registry.Find(name) is { IsAsync: true })
                return name;

        return null;
    }
}
