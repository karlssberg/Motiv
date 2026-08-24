namespace Motiv.Serialization;

/// <summary>
/// A named, hot-swappable, async spec-flavoured rule. Evaluations forward the underlying
/// spec's <see cref="ValueTask{TResult}"/> directly off an immutable snapshot — forwarded
/// without an intermediate state machine, and in-flight evaluations always complete
/// against a coherent version. A parallel hierarchy to <see cref="Rule{TModel,TMetadata}"/>
/// over <see cref="AsyncSpecBase{TModel,TMetadata}"/>. Declare rules as sealed subclasses so
/// the type itself is the identity:
/// <code>public sealed class CreditCheckRule() : AsyncRule&lt;Customer, string&gt;("credit-check", SomeAsyncSpec);</code>
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class AsyncRule<TModel, TMetadata> : RuleBase
{
    private protected sealed class State(
        string? documentJson, int version, AsyncSpecBase<TModel, TMetadata> spec, bool audited = false)
    {
        public string? DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public AsyncSpecBase<TModel, TMetadata> Spec { get; } = spec;

        /// <summary>
        /// Whether every evaluation against this binding is recorded. Read off the document once, at
        /// bind time, rather than re-parsed per evaluation. Always false for a compiled default, which
        /// has no document to carry the flag.
        /// </summary>
        public bool Audited { get; } = audited;

        private IReadOnlyList<PropositionVersion>? _propositionPin;

        /// <summary>
        /// Every authored proposition this binding resolves through, transitively, at the version it
        /// had when the binding was made — a decision record's third anchor.
        /// </summary>
        /// <remarks>
        /// Computed once per state rather than once per evaluation, and that is sound rather than a
        /// shortcut: republishing anything in the closure rebinds every referrer and produces a
        /// <em>new</em> state, so this list cannot go stale while the state it belongs to is live.
        /// Deferred to the first audited evaluation so an unaudited rule never pays for it.
        /// </remarks>
        public IReadOnlyList<PropositionVersion> PropositionPin(ScopeGeneration generation, string ruleName) =>
            _propositionPin ??= ResolvePin(generation, ruleName);

        private static IReadOnlyList<PropositionVersion> ResolvePin(ScopeGeneration generation, string ruleName)
        {
            var references = generation.Graph.ReferenceClosure(NodeId.Rule(ruleName));
            if (references.Count == 0)
                return [];

            var pinned = new List<PropositionVersion>(references.Count);
            foreach (var reference in references)
            {
                // A name resolving to a compiled spec rather than an authored proposition has no
                // version of its own; BuildId is what pins those, which is why it is a separate anchor.
                if (generation.Authored.TryGetValue(reference, out var authored))
                    pinned.Add(new PropositionVersion(authored.Name, authored.Version));
            }

            return pinned;
        }
    }

    /// <summary>Creates an async rule whose default implementation is a compiled async spec.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultSpec">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncRule(string name, AsyncSpecBase<TModel, TMetadata> defaultSpec, string? description = null)
        : base(name, RuleDefault.Compiled(defaultSpec ?? throw new ArgumentNullException(nameof(defaultSpec))), description)
    {
    }

    /// <summary>Creates an async rule whose default implementation is a serialized rule document, bound at <see cref="RuleSet.Add"/>.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON (e.g. from <see cref="RuleDocuments.Embedded(string)"/>).</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncRule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, RuleDefault.Document((defaultDocument ?? throw new ArgumentNullException(nameof(defaultDocument))).Json), description)
    {
    }

    /// <inheritdoc />
    public override Type ModelType => typeof(TModel);

    /// <inheritdoc />
    public override Type MetadataType => typeof(TMetadata);

    /// <inheritdoc />
    public override bool IsAsync => true;

    /// <inheritdoc />
    public override bool IsPolicy => false;

    /// <inheritdoc />
    public override int Version => Live().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Live().DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    /// <remarks>
    /// Reads the <em>pinned</em> world when a <c>DecisionSnapshot</c> is open, so several rules
    /// evaluated inside one decision resolve against one published set rather than one each.
    /// </remarks>
    public ValueTask<BooleanResultBase<TMetadata>> EvaluateAsync(
        TModel model, CancellationToken cancellationToken = default)
    {
        // Not an async method. Two things depend on that: an unbound rule must throw synchronously,
        // as it always has, rather than handing back a faulted task nobody awaits; and an unaudited
        // rule must keep forwarding the underlying ValueTask untouched, with no state machine between
        // it and its caller. Only an audited evaluation pays for the wrapper.
        var generation = Scope.Active;
        var state = StateIn(generation);
        var evaluation = state.Spec.EvaluateAsync(model, cancellationToken);

        return state.Audited && DecisionLog is not null
            ? RecordAsync(state, generation, model, evaluation)
            : evaluation;
    }

    private async ValueTask<BooleanResultBase<TMetadata>> RecordAsync(
        State state,
        ScopeGeneration generation,
        TModel model,
        ValueTask<BooleanResultBase<TMetadata>> evaluation)
    {
        var result = await evaluation.ConfigureAwait(false);
        Record(state, generation, model, result);
        return result;
    }

    /// <summary>
    /// Writes this evaluation to the decision log, when the binding asked to be recorded. Shared by
    /// every entry point on this rule flavour, including the policy shadow — a rule that says it is
    /// audited and records only through one of its two methods is worse than one that records
    /// nothing, because the gap is invisible.
    /// </summary>
    /// <remarks>
    /// The outcome is projected here, on the awaiting flow, rather than on the writer. Deferring it
    /// would move real cost off the request path, but the result tree memoises as it is read and none
    /// of that memoisation is documented thread-safe: handing a half-read result to a background
    /// writer races the caller still reading it, in the one subsystem whose output is the product.
    /// What crosses the queue is immutable.
    /// </remarks>
    private protected void Record(
        State state, ScopeGeneration generation, TModel model, BooleanResultBase<TMetadata> result)
    {
        if (!state.Audited || DecisionLog is not { } log)
            return;

        var decision = DecisionSnapshot.Current;
        log.Enqueue(new DecisionRecord(
            Id: Guid.NewGuid(),
            CorrelationId: decision?.CorrelationId ?? Guid.NewGuid().ToString("N"),
            TimestampUtc: DateTimeOffset.UtcNow,
            Caller: decision?.Caller,
            RuleName: Name,
            RuleVersion: state.Version,
            BuildId: BuildIdentity.Current,
            ReferencedPropositionVersions: state.PropositionPin(generation, Name),
            Input: log.Capture.Capture(model),
            Outcome: ResultProjection.ProjectUntyped(result)));
    }

    /// <summary>The live state — what an administrative read or a publish must see, pinned or not.</summary>
    private protected State Live() => StateIn(Scope.Current);

    /// <summary>This rule's state in <paramref name="generation"/>, or null when it has none there.</summary>
    /// <remarks>
    /// The bounds check is not redundant: Add claims the slot before it publishes the state into it,
    /// so a concurrent read in that window must still get an answer rather than an
    /// IndexOutOfRangeException.
    /// </remarks>
    private protected State? FindStateIn(ScopeGeneration generation)
    {
        var slots = generation.RuleSlots;
        return Slot >= 0 && Slot < slots.Length ? slots[Slot]?.State as State : null;
    }

    private protected State StateIn(ScopeGeneration generation) =>
        FindStateIn(generation)
        ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    internal sealed override object BindDefaultState(RuleSerializer serializer) => BindDefault(serializer);

    internal sealed override object WithVersion(object state, int version)
    {
        var current = (State)state;
        return new State(current.DocumentJson, version, current.Spec, current.Audited);
    }

    /// <inheritdoc />
    internal sealed override string? DocumentJsonIn(ScopeGenerationBuilder builder) =>
        builder.FindRuleState(Slot) is State state ? state.DocumentJson : null;

    /// <inheritdoc />
    internal sealed override string? DocumentJsonIn(ScopeGeneration generation) =>
        // See the synchronous twin in Rule<TModel, TMetadata> for why this is null-tolerant rather
        // than routed through StateIn.
        FindStateIn(generation)?.DocumentJson;

    /// <inheritdoc />
    internal sealed override object? BindStoredState(
        RuleSerializer serializer, string? documentJson, int version, List<RuleError> errors)
    {
        if (documentJson is not null)
        {
            return TryBindState(serializer, documentJson, version, errors);
        }

        // A recorded revert: the rule is on its default at the store's version. See the synchronous
        // twin in Rule<TModel, TMetadata> for why the throw is caught rather than allowed to escape.
        try
        {
            var @default = BindDefault(serializer);
            return new State(@default.DocumentJson, version, @default.Spec, @default.Audited);
        }
        catch (RuleSerializationException exception)
        {
            errors.AddRange(exception.Errors);
            return null;
        }
    }

    internal sealed override RulePrepareResult PrepareUpdate(
        RuleSerializer serializer, string documentJson, int expectedVersion)
    {
        var current = Live();
        if (current.Version != expectedVersion)
            return RulePrepareResult.VersionConflict(current.Version);

        var errors = new List<RuleError>();
        if (TryBindState(serializer, documentJson, current.Version + 1, errors) is not { } prepared)
            return RulePrepareResult.Invalid(errors);

        return RulePrepareResult.Prepared(new Publication(this, prepared));
    }

    internal sealed override RulePrepareResult PrepareRevert(RuleSerializer serializer, int expectedVersion)
    {
        var current = Live();
        if (current.Version != expectedVersion)
            return RulePrepareResult.VersionConflict(current.Version);

        State @default;
        try
        {
            @default = BindDefault(serializer);
        }
        catch (RuleSerializationException ex)
        {
            return RulePrepareResult.Invalid(ex.Errors);
        }

        return RulePrepareResult.Prepared(new Publication(
            this, new State(@default.DocumentJson, current.Version + 1, @default.Spec, @default.Audited)));
    }

    internal sealed override void ValidateDocument(
        RuleSerializer serializer, string documentJson, List<RuleError> errors) =>
        // The bound state is discarded — this is the dry run, and the errors list is the whole answer.
        // The version is arbitrary for the same reason: nothing here is published.
        TryBindState(serializer, documentJson, version: 0, errors);

    internal sealed override (int Version, string? DocumentJson) VersionedDocument()
    {
        // Active, not Current — this serves the catalog listing, which is a read for display and
        // therefore the pinned side of the split. See RuleBase.VersionedDocument.
        var listed = StateIn(Scope.Active);
        return (listed.Version, listed.DocumentJson);
    }

    internal sealed override IRebindCommit? PrepareRebind(
        RuleSerializer serializer, ScopeGeneration world, List<RuleError> errors)
    {
        // The walk's world, not Live(): a second read of Scope.Current would let a publish landing
        // mid-walk have this rebind a node one world names against a definition from another.
        var current = StateIn(world);

        // A compiled default resolves no names, so there is nothing to rebind.
        if (current.DocumentJson is null)
            return NoRebindCommit.Instance;

        if (TryBindState(serializer, current.DocumentJson, current.Version, errors) is not { } rebound)
            return null;

        // The version is carried across unchanged: the document did not change, only what it resolves
        // to, so bumping it would spuriously conflict with an editor's open draft.
        return new RebindCommit(this, rebound);
    }

    /// <summary>
    /// A prepared rebind of this rule, published by writing its binding into the successor. A binding
    /// write, not a state write: a rebind must not clear a quarantine — see
    /// <see cref="RuleSlot.WithBinding"/>.
    /// </summary>
    private sealed class RebindCommit(AsyncRule<TModel, TMetadata> rule, State replacement) : IRebindCommit
    {
        public void ApplyTo(ScopeGenerationBuilder builder) => builder.SetRuleBinding(rule.Slot, replacement);
    }

    /// <summary>
    /// A prepared rule change, published by writing its state into the successor generation. No
    /// compare-and-swap: the outer gate serialises whole operations, so no second writer is in flight,
    /// and a CAS could never see a different process anyway. Enforcement lives in the store's
    /// <c>(Name, Version)</c> primary key, which can.
    /// </summary>
    private sealed class Publication(AsyncRule<TModel, TMetadata> rule, State replacement) : IRulePublication
    {
        public int Version => replacement.Version;

        public string? DocumentJson => replacement.DocumentJson;

        public void ApplyTo(ScopeGenerationBuilder builder) => builder.SetRuleState(rule.Slot, replacement);
    }

    private State BindDefault(RuleSerializer serializer)
    {
        if (Default.CompiledSpec is not null)
            return new State(null, 1, (AsyncSpecBase<TModel, TMetadata>)Default.CompiledSpec);

        var spec = Bind(serializer, Default.DocumentJson!);
        if (RequirePolicy(spec) is { } policyError)
            throw new RuleSerializationException([policyError]);

        var audited = serializer.IsAudited(Default.DocumentJson!);
        if (RequireAuditCapture(audited) is { } auditError)
            throw new RuleSerializationException([auditError]);

        return new State(Default.DocumentJson, 1, spec, audited);
    }

    /// <summary>
    /// Binds a document, applies the flavour and audit checks, and assembles the state they produce —
    /// collecting every reason it would not bind into <paramref name="errors"/>. The one failure shape
    /// behind the four callers that report a bad document rather than throwing on one; each then says
    /// so in its own terms.
    /// </summary>
    /// <returns>The bound state, or null when it did not bind, in which case <paramref name="errors"/> says why.</returns>
    private State? TryBindState(
        RuleSerializer serializer, string documentJson, int version, List<RuleError> errors)
    {
        AsyncSpecBase<TModel, TMetadata> spec;
        try
        {
            spec = Bind(serializer, documentJson);
        }
        catch (RuleSerializationException exception)
        {
            errors.AddRange(exception.Errors);
            return null;
        }

        if (RequirePolicy(spec) is { } policyError)
        {
            errors.Add(policyError);
            return null;
        }

        var audited = serializer.IsAudited(documentJson);
        if (RequireAuditCapture(audited) is { } auditError)
        {
            errors.Add(auditError);
            return null;
        }

        return new State(documentJson, version, spec, audited);
    }

    /// <summary>
    /// Refuses an audited document the host has decided nothing about. Capture has no default by
    /// design, so the refusal is where the absence of a decision becomes visible — and putting it at
    /// bind time puts it in three places for the price of one: a governed publish is rejected with a
    /// readable reason, a startup load reports it, and a replica deployed without the posture
    /// quarantines the rule and says why rather than silently logging whatever the model holds.
    /// </summary>
    private RuleError? RequireAuditCapture(bool audited)
    {
        if (!audited)
            return null;

        if (DecisionLog is null)
            return new RuleError("$.audited", RuleErrorCode.AuditCaptureNotConfigured,
                $"rule '{Name}' is marked audited, but its RuleSet was built without a DecisionLog; " +
                "pass one to the RuleSet constructor");

        if (!DecisionLog.Capture.Covers(typeof(TModel)))
            return new RuleError("$.audited", RuleErrorCode.AuditCaptureNotConfigured,
                $"rule '{Name}' is marked audited, but no capture posture is registered for " +
                $"'{typeof(TModel).Name}'; choose one with DecisionLogOptions.Capture — " +
                "ReferenceOnly is recommended for production");

        return null;
    }

    private protected virtual AsyncSpecBase<TModel, TMetadata> Bind(RuleSerializer serializer, string documentJson) =>
        serializer.DeserializeAsyncSpec<TModel, TMetadata>(documentJson);

    /// <summary>Policy-flavoured subclasses override to reject non-policy documents; specs accept anything.</summary>
    private protected virtual RuleError? RequirePolicy(AsyncSpecBase<TModel, TMetadata> spec) => null;
}
