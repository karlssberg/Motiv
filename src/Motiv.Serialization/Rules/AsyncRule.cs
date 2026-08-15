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
    private protected sealed class State(string? documentJson, int version, AsyncSpecBase<TModel, TMetadata> spec)
    {
        public string? DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public AsyncSpecBase<TModel, TMetadata> Spec { get; } = spec;
    }

    private State? _state;

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
    public override int Version => Snapshot().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Snapshot().DocumentJson;

    /// <inheritdoc />
    internal sealed override string? DocumentJsonIn(ScopeGenerationBuilder builder) => DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    public ValueTask<BooleanResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
        Snapshot().Spec.EvaluateAsync(model, cancellationToken);

    private protected State Snapshot() =>
        Volatile.Read(ref _state)
        ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    internal sealed override void Attach(RuleSerializer serializer)
    {
        if (Volatile.Read(ref _state) is not null)
            throw new InvalidOperationException($"Rule '{Name}' has already been added to a RuleSet.");

        Volatile.Write(ref _state, BindDefault(serializer));
    }

    internal sealed override RulePrepareResult PrepareUpdate(
        RuleSerializer serializer, string documentJson, int expectedVersion)
    {
        var current = Snapshot();
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
        var current = Snapshot();
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
        var snapshot = Snapshot();
        return (snapshot.Version, snapshot.DocumentJson);
    }

    internal sealed override void RestoreVersion(int version)
    {
        var current = Snapshot();
        Volatile.Write(ref _state, new State(current.DocumentJson, version, current.Spec));
    }

    internal sealed override IRebindCommit? PrepareRebind(RuleSerializer serializer, List<RuleError> errors)
    {
        var current = Snapshot();

        // A compiled default resolves no names, so there is nothing to rebind.
        if (current.DocumentJson is null)
            return NoRebindCommit.Instance;

        if (TryBind(serializer, current.DocumentJson, errors) is not { } spec)
            return null;

        // The version is carried across unchanged: the document did not change, only what it resolves
        // to, so bumping it would spuriously conflict with an editor's open draft.
        return new RebindCommit(this, new State(current.DocumentJson, current.Version, spec));
    }

    /// <summary>A prepared rebind of this rule, published by swapping its state snapshot.</summary>
    private sealed class RebindCommit(AsyncRule<TModel, TMetadata> rule, State replacement) : IRebindCommit
    {
        // A rule is not referenceable from a document, so it contributes nothing to the world being
        // built. Its binding is still a field of its own, so the swap below is all there is to do.
        public void ApplyTo(ScopeGenerationBuilder builder)
        {
        }

        public void Commit() => Volatile.Write(ref rule._state, replacement);
    }

    /// <summary>
    /// A prepared rule change, published by swapping the state snapshot. A plain
    /// <see cref="Volatile.Write{T}"/>, not a compare-and-swap: the outer gate on
    /// <see cref="BindingScope"/> serialises whole operations, so no second writer can be in flight,
    /// and a CAS was never able to see a <em>different process</em> anyway. Enforcement lives in the
    /// store's <c>(Name, Version)</c> primary key, which can.
    /// </summary>
    private sealed class Publication(AsyncRule<TModel, TMetadata> rule, State replacement) : IRulePublication
    {
        public int Version => replacement.Version;

        public string? DocumentJson => replacement.DocumentJson;

        public void ApplyTo(ScopeGenerationBuilder builder) => Volatile.Write(ref rule._state, replacement);
    }

    private State BindDefault(RuleSerializer serializer)
    {
        if (Default.CompiledSpec is not null)
            return new State(null, 1, (AsyncSpecBase<TModel, TMetadata>)Default.CompiledSpec);

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
    private AsyncSpecBase<TModel, TMetadata>? TryBind(
        RuleSerializer serializer, string documentJson, List<RuleError> errors)
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

        if (RequirePolicy(spec) is not { } policyError)
            return spec;

        errors.Add(policyError);
        return null;
    }

    private protected virtual AsyncSpecBase<TModel, TMetadata> Bind(RuleSerializer serializer, string documentJson) =>
        serializer.DeserializeAsyncSpec<TModel, TMetadata>(documentJson);

    /// <summary>Policy-flavoured subclasses override to reject non-policy documents; specs accept anything.</summary>
    private protected virtual RuleError? RequirePolicy(AsyncSpecBase<TModel, TMetadata> spec) => null;
}
