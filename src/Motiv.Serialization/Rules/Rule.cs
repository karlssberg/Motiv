namespace Motiv.Serialization;

/// <summary>
/// A named, hot-swappable, spec-flavoured rule: evaluations read an immutable snapshot, so an
/// in-flight evaluation always completes against a coherent version even while the rule is being
/// replaced. Declare rules as sealed subclasses so the type itself is the identity:
/// <code>public sealed class CanCheckoutRule() : Rule&lt;Customer, string&gt;("can-checkout", SomeSpec);</code>
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class Rule<TModel, TMetadata> : RuleBase
{
    private protected sealed class State(string? documentJson, int version, SpecBase<TModel, TMetadata> spec)
    {
        public string? DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public SpecBase<TModel, TMetadata> Spec { get; } = spec;
    }

    /// <summary>Creates a rule whose default implementation is a compiled spec.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultSpec">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public Rule(string name, SpecBase<TModel, TMetadata> defaultSpec, string? description = null)
        : base(name, RuleDefault.Compiled(defaultSpec ?? throw new ArgumentNullException(nameof(defaultSpec))), description)
    {
    }

    /// <summary>Creates a rule whose default implementation is a serialized rule document, bound at <see cref="RuleSet.Add"/>.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON (e.g. from <see cref="RuleDocuments.Embedded(string)"/>).</param>
    /// <param name="description">An optional human-readable description.</param>
    public Rule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, RuleDefault.Document((defaultDocument ?? throw new ArgumentNullException(nameof(defaultDocument))).Json), description)
    {
    }

    /// <inheritdoc />
    public override Type ModelType => typeof(TModel);

    /// <inheritdoc />
    public override Type MetadataType => typeof(TMetadata);

    /// <inheritdoc />
    public override bool IsAsync => false;

    /// <inheritdoc />
    public override bool IsPolicy => false;

    /// <inheritdoc />
    public override int Version => Live().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Live().DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    /// <remarks>
    /// Reads the <em>pinned</em> world when a <c>DecisionSnapshot</c> is open, so several rules
    /// evaluated inside one decision resolve against one published set rather than one each.
    /// </remarks>
    public BooleanResultBase<TMetadata> Evaluate(TModel model) => StateIn(Scope.Active).Spec.Evaluate(model);

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
        return new State(current.DocumentJson, version, current.Spec);
    }

    /// <inheritdoc />
    internal sealed override string? DocumentJsonIn(ScopeGenerationBuilder builder) =>
        builder.FindRuleState(Slot) is State state ? state.DocumentJson : null;

    /// <inheritdoc />
    internal sealed override string? DocumentJsonIn(ScopeGeneration generation) =>
        // Null-tolerant rather than routed through StateIn, which throws: a world snapshotted before
        // this rule was registered simply has no slot for it, and that is an answer.
        FindStateIn(generation)?.DocumentJson;

    /// <inheritdoc />
    internal sealed override object? BindStoredState(
        RuleSerializer serializer, string? documentJson, int version, List<RuleError> errors)
    {
        if (documentJson is not null)
        {
            return TryBind(serializer, documentJson, errors) is { } spec
                ? new State(documentJson, version, spec)
                : null;
        }

        // A recorded revert: the rule is on its default at the store's version. A compiled default
        // cannot fail, but a RuleDocumentSource one can — an authored proposition it references may
        // have been quarantined by the very refresh calling this — and a poller must be told rather
        // than thrown at.
        try
        {
            var @default = BindDefault(serializer);
            return new State(@default.DocumentJson, version, @default.Spec);
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
        if (TryBind(serializer, documentJson, errors) is not { } spec)
            return RulePrepareResult.Invalid(errors);

        return RulePrepareResult.Prepared(
            new Publication(this, new State(documentJson, current.Version + 1, spec)));
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

        return RulePrepareResult.Prepared(
            new Publication(this, new State(@default.DocumentJson, current.Version + 1, @default.Spec)));
    }

    internal sealed override void ValidateDocument(
        RuleSerializer serializer, string documentJson, List<RuleError> errors) =>
        // The bound spec is discarded — this is the dry run, and the errors list is the whole answer.
        TryBind(serializer, documentJson, errors);

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

        if (TryBind(serializer, current.DocumentJson, errors) is not { } spec)
            return null;

        // The version is carried across unchanged: the document did not change, only what it resolves
        // to, so bumping it would spuriously conflict with an editor's open draft.
        return new RebindCommit(this, new State(current.DocumentJson, current.Version, spec));
    }

    /// <summary>
    /// A prepared rebind of this rule, published by writing its binding into the successor. A binding
    /// write, not a state write: a rebind must not clear a quarantine — see
    /// <see cref="RuleSlot.WithBinding"/>.
    /// </summary>
    private sealed class RebindCommit(Rule<TModel, TMetadata> rule, State replacement) : IRebindCommit
    {
        public void ApplyTo(ScopeGenerationBuilder builder) => builder.SetRuleBinding(rule.Slot, replacement);
    }

    /// <summary>
    /// A prepared rule change, published by writing its state into the successor generation. No
    /// compare-and-swap: the outer gate serialises whole operations, so no second writer is in flight,
    /// and a CAS could never see a different process anyway. Enforcement lives in the store's
    /// <c>(Name, Version)</c> primary key, which can.
    /// </summary>
    private sealed class Publication(Rule<TModel, TMetadata> rule, State replacement) : IRulePublication
    {
        public int Version => replacement.Version;

        public string? DocumentJson => replacement.DocumentJson;

        public void ApplyTo(ScopeGenerationBuilder builder) => builder.SetRuleState(rule.Slot, replacement);
    }

    private State BindDefault(RuleSerializer serializer)
    {
        if (Default.CompiledSpec is not null)
            return new State(null, 1, (SpecBase<TModel, TMetadata>)Default.CompiledSpec);

        var spec = Bind(serializer, Default.DocumentJson!);
        if (RequirePolicy(spec) is { } policyError)
            throw new RuleSerializationException([policyError]);
        return new State(Default.DocumentJson, 1, spec);
    }

    /// <summary>
    /// Binds a document and applies the flavour check, collecting every reason it would not bind into
    /// <paramref name="errors"/>. The one failure shape behind the three callers that report a bad
    /// document rather than throwing on one — each then says so in its own terms.
    /// </summary>
    /// <returns>The bound spec, or null when it did not bind, in which case <paramref name="errors"/> says why.</returns>
    private SpecBase<TModel, TMetadata>? TryBind(
        RuleSerializer serializer, string documentJson, List<RuleError> errors)
    {
        SpecBase<TModel, TMetadata> spec;
        try
        {
            spec = Bind(serializer, documentJson);
        }
        catch (RuleSerializationException exception)
        {
            errors.AddRange(exception.Errors);
            return null;
        }

        if (RequirePolicy(spec) is not { } policyError)
            return spec;

        errors.Add(policyError);
        return null;
    }

    private protected virtual SpecBase<TModel, TMetadata> Bind(RuleSerializer serializer, string documentJson) =>
        serializer.Deserialize<TModel, TMetadata>(documentJson);

    /// <summary>Policy-flavoured subclasses override to reject non-policy documents; specs accept anything.</summary>
    private protected virtual RuleError? RequirePolicy(SpecBase<TModel, TMetadata> spec) => null;
}
