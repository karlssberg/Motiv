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

    private State? _state;

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
    public override int Version => Snapshot().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Snapshot().DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    public BooleanResultBase<TMetadata> Evaluate(TModel model) => Snapshot().Spec.Evaluate(model);

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

    internal sealed override RuleUpdateResult TryUpdate(RuleSerializer serializer, string documentJson, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RuleUpdateResult.VersionConflict(current.Version);

        SpecBase<TModel, TMetadata> spec;
        try
        {
            spec = Bind(serializer, documentJson);
        }
        catch (RuleSerializationException ex)
        {
            return RuleUpdateResult.Invalid(ex.Errors);
        }

        if (RequirePolicy(spec) is { } policyError)
            return RuleUpdateResult.Invalid([policyError]);

        return Publish(current, new State(documentJson, current.Version + 1, spec));
    }

    internal sealed override RuleUpdateResult TryRevert(RuleSerializer serializer, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RuleUpdateResult.VersionConflict(current.Version);

        State @default;
        try
        {
            @default = BindDefault(serializer);
        }
        catch (RuleSerializationException ex)
        {
            return RuleUpdateResult.Invalid(ex.Errors);
        }

        return Publish(current, new State(@default.DocumentJson, current.Version + 1, @default.Spec));
    }

    internal sealed override (int Version, string? DocumentJson) VersionedDocument()
    {
        var snapshot = Snapshot();
        return (snapshot.Version, snapshot.DocumentJson);
    }

    private RuleUpdateResult Publish(State expected, State replacement)
    {
        var witnessed = Interlocked.CompareExchange(ref _state, replacement, expected);
        return ReferenceEquals(witnessed, expected)
            ? RuleUpdateResult.Updated(replacement.Version)
            : RuleUpdateResult.VersionConflict(witnessed!.Version);
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

    private protected virtual SpecBase<TModel, TMetadata> Bind(RuleSerializer serializer, string documentJson) =>
        serializer.Deserialize<TModel, TMetadata>(documentJson);

    /// <summary>Policy-flavoured subclasses override to reject non-policy documents; specs accept anything.</summary>
    private protected virtual RuleError? RequirePolicy(SpecBase<TModel, TMetadata> spec) => null;
}
