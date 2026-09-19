using System.Reflection;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization;

/// <summary>
/// Loads externalized JSON rule documents into Motiv specs, resolving leaf references against an
/// <see cref="ISpecSource" /> — a <see cref="SpecRegistry" /> on its own, or a layered source in
/// which runtime-authored propositions shadow and extend the compiled catalog.
/// </summary>
public sealed class RuleSerializer
{
    private readonly ISpecSource _source;
    private readonly RuleSerializerOptions _options;

    /// <summary>Creates a serializer that resolves spec references against the given registry.</summary>
    /// <param name="registry">The registry used to resolve spec references.</param>
    /// <param name="options">Options controlling validation and loading; defaults are used when omitted.</param>
    public RuleSerializer(SpecRegistry registry, RuleSerializerOptions? options = null)
        : this((ISpecSource)(registry ?? throw new ArgumentNullException(nameof(registry))), options)
    {
    }

    /// <summary>
    /// Creates a serializer over a layered source, so runtime-authored propositions shadow and
    /// extend the compiled registry without the binders distinguishing the two.
    /// </summary>
    internal RuleSerializer(ISpecSource source, RuleSerializerOptions? options = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? new RuleSerializerOptions();
    }

    /// <summary>
    /// Checks a rule document for structural errors without loading it. Semantic checks that need a
    /// model type (registry lookups, type matching) are not performed.
    /// </summary>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All structural errors found, or an empty list when the document is well-formed.</returns>
    public IReadOnlyList<RuleError> Validate(string json)
    {
        var errors = new List<RuleError>();
        new RuleDocumentParser(_options).Parse(json, errors);
        return errors;
    }

    /// <summary>
    /// Loads a rule document into an explanation spec, resolving spec references against the
    /// registry. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, string> Deserialize<TModel>(string json) =>
        Deserialize<TModel>(json, (IReadOnlyDictionary<string, object?>?)null);

    /// <summary>
    /// Loads a rule document into an explanation spec, resolving spec references against the
    /// registry and supplying parameter values from an anonymous object. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">An object whose public properties supply parameter values, or <c>null</c>.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, string> Deserialize<TModel>(string json, object? parameters) =>
        Deserialize<TModel>(json, RuleParameterResolver.ToDictionary(parameters));

    /// <summary>
    /// Loads a rule document into an explanation spec, resolving spec references against the
    /// registry and supplying parameter values from a dictionary. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">Parameter values keyed by declared parameter name, or <c>null</c>.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, string> Deserialize<TModel>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = RuleBinder.Bind<TModel>(document!, _source, _options, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    /// <summary>
    /// Loads a rule document into an asynchronous explanation spec, resolving spec references
    /// against the registry. Sync references are lifted; async references are used directly.
    /// Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json) =>
        DeserializeAsyncSpec<TModel>(json, (IReadOnlyDictionary<string, object?>?)null);

    /// <summary>
    /// Loads a rule document into an asynchronous explanation spec, resolving spec references
    /// against the registry and supplying parameter values from an anonymous object. Sync
    /// references are lifted; async references are used directly. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">An object whose public properties supply parameter values, or <c>null</c>.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json, object? parameters) =>
        DeserializeAsyncSpec<TModel>(json, RuleParameterResolver.ToDictionary(parameters));

    /// <summary>
    /// Loads a rule document into an asynchronous explanation spec, resolving spec references
    /// against the registry and supplying parameter values from a dictionary. Sync references are
    /// lifted; async references are used directly. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">Parameter values keyed by declared parameter name, or <c>null</c>.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = AsyncRuleBinder.Bind<TModel>(document!, _source, _options, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    /// <summary>
    /// Loads a rule document into a typed metadata spec, resolving spec references against the
    /// registry. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(string json) =>
        Deserialize<TModel, TMetadata>(json, (IReadOnlyDictionary<string, object?>?)null);

    /// <summary>
    /// Loads a rule document into a typed metadata spec, resolving spec references against the
    /// registry and supplying parameter values from an anonymous object. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">An object whose public properties supply parameter values, or <c>null</c>.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(string json, object? parameters) =>
        Deserialize<TModel, TMetadata>(json, RuleParameterResolver.ToDictionary(parameters));

    /// <summary>
    /// Loads a rule document into a typed metadata spec, resolving spec references against the
    /// registry and supplying parameter values from a dictionary. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">Parameter values keyed by declared parameter name, or <c>null</c>.</param>
    /// <returns>The composed spec, behaviorally identical to its fluent-built equivalent.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public SpecBase<TModel, TMetadata> Deserialize<TModel, TMetadata>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (typeof(TMetadata) == typeof(string))
            return (SpecBase<TModel, TMetadata>)(object)Deserialize<TModel>(json, parameters);

        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = new MetadataRuleBinder<TMetadata>(_source, _options).Bind<TModel>(document!, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    /// <summary>
    /// Loads a rule document into an asynchronous typed-metadata spec, resolving spec references
    /// against the registry. Sync references are lifted; async references are used directly.
    /// Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json) =>
        DeserializeAsyncSpec<TModel, TMetadata>(json, (IReadOnlyDictionary<string, object?>?)null);

    /// <summary>
    /// Loads a rule document into an asynchronous typed-metadata spec, resolving spec references
    /// against the registry and supplying parameter values from an anonymous object. Sync
    /// references are lifted; async references are used directly. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">An object whose public properties supply parameter values, or <c>null</c>.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json, object? parameters) =>
        DeserializeAsyncSpec<TModel, TMetadata>(json, RuleParameterResolver.ToDictionary(parameters));

    /// <summary>
    /// Loads a rule document into an asynchronous typed-metadata spec, resolving spec references
    /// against the registry and supplying parameter values from a dictionary. Sync references are
    /// lifted; async references are used directly. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <param name="parameters">Parameter values keyed by declared parameter name, or <c>null</c>.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (typeof(TMetadata) == typeof(string))
            return (AsyncSpecBase<TModel, TMetadata>)(object)DeserializeAsyncSpec<TModel>(json, parameters);

        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = new AsyncMetadataRuleBinder<TMetadata>(_source, _options).Bind<TModel>(document!, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    /// <summary>
    /// Checks a rule document structurally and semantically against the registry for an
    /// explanation load, accumulating every error instead of throwing. Parameter values are not
    /// taken: required parameters are stood in by type-shaped placeholders, so supply errors are
    /// only reported by <see cref="Deserialize{TModel}(string)" />.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All errors found, or an empty list when the document would load.</returns>
    public IReadOnlyList<RuleError> Validate<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            RuleBinder.Bind<TModel>(document, _source, _options, errors);
        return errors;
    }

    /// <summary>
    /// Checks a rule document as <see cref="Deserialize{TModel}(string)" /> does — so, unlike
    /// <see cref="Validate{TModel}(string)" />, an unsupplied required parameter is reported as
    /// <see cref="RuleErrorCode.MissingParameter" /> rather than stood in by a placeholder — and
    /// also reports what the checker learned about every expression leaf reached while checking the
    /// document: the types it solved for literals and parameters, each leaf's result type, and its
    /// warnings. A leaf inside a quantifier body is analysed against the collection's element type,
    /// not <typeparamref name="TModel"/>.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to inspect.</param>
    /// <returns>The document's errors and the facts gathered about its expression leaves.</returns>
    public RuleValidation Inspect<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = Prepare(json, null, errors);
        if (document is null)
            return new RuleValidation(errors, []);

        // Unlike Validate<TModel>, binding here is not gated on errors.Count == 0: a document with
        // parameter-supply errors still binds and analyses as far as it can, so facts surface
        // alongside those errors instead of being withheld by them.
        if (document.Root is not null)
            RuleBinder.Bind<TModel>(document, _source, _options, errors);

        // One shared visited set across both walks: a Local node's body is the very same RuleNode
        // instance LocalResolver.Link assigned to document.Definitions, so a root walk that recurses
        // into it and the definitions loop below would otherwise gather every fact in that body
        // twice (once per reference, for a definition referenced more than once).
        // RuleNode is a plain class with no Equals/GetHashCode override, so a bare HashSet<RuleNode>
        // already compares by reference — exactly what's needed to de-duplicate the shared instance
        // LocalResolver.Link assigns to every "local" node naming the same definition.
        var facts = new List<RuleLeafFact>();
        var visited = new HashSet<RuleNode>();
        if (document.Root is not null)
            CollectFacts<TModel>(document.Root, facts, visited);
        foreach (var definition in document.Definitions)
            CollectFacts<TModel>(definition, facts, visited);
        return new RuleValidation(errors, facts);
    }

    private static readonly MethodInfo CollectFactsMethod = typeof(RuleSerializer)
        .GetMethod(nameof(CollectFacts), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Walks a rule subtree gathering leaf facts, switching to a collection's element type when it
    /// descends into a higher-order node's body — mirroring how <see cref="RuleBinder.BindHigherOrder{TModel}" />
    /// (via <c>CollectionBinding</c>) rebinds a quantifier body against the element rather than the parent.
    /// <paramref name="visited"/> is shared across the whole document so a definition's body — reached
    /// both by recursing into a <see cref="RuleOperator.Local" /> node and by <see cref="Inspect{TModel}"/>'s
    /// own pass over <c>document.Definitions</c> — contributes its facts only once.
    /// </summary>
    private void CollectFacts<TModel>(RuleNode node, List<RuleLeafFact> facts, HashSet<RuleNode> visited)
    {
        if (!visited.Add(node))
            return;

        switch (node.Operator)
        {
            case RuleOperator.Expression:
                var analysis = LeafBinding.Analyse<TModel>(node, []);
                if (analysis is not null)
                    facts.AddRange(LeafBinding.FactsOf(node, analysis));
                return;
            case RuleOperator.Local:
                if (node.Definition is not null)
                    CollectFacts<TModel>(node.Definition, facts, visited);
                return;
        }

        if (node.Operator.IsHigherOrder())
        {
            if (node.Children.Count == 0)
                return;
            var binding = _source.FindCollection<TModel>(node.PathText!);
            if (binding is null)
                return;
            CollectFactsMethod.MakeGenericMethod(binding.ElementType)
                .Invoke(this, [node.Children[0], facts, visited]);
            return;
        }

        foreach (var child in node.Children)
            CollectFacts<TModel>(child, facts, visited);
    }

    /// <summary>
    /// Checks a rule document structurally and semantically against the registry for a metadata
    /// load, accumulating every error instead of throwing. Parameter values are not taken:
    /// required parameters are stood in by type-shaped placeholders, so supply errors are only
    /// reported by <see cref="Deserialize{TModel, TMetadata}(string)" />.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All errors found, or an empty list when the document would load.</returns>
    public IReadOnlyList<RuleError> Validate<TModel, TMetadata>(string json)
    {
        if (typeof(TMetadata) == typeof(string))
            return Validate<TModel>(json);

        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            new MetadataRuleBinder<TMetadata>(_source, _options).Bind<TModel>(document, errors);
        return errors;
    }

    /// <summary>
    /// Checks a rule document structurally and semantically against the registry for an
    /// asynchronous explanation load, accumulating every error instead of throwing. Parameter
    /// values are not taken: required parameters are stood in by type-shaped placeholders, so
    /// supply errors are only reported by <see cref="DeserializeAsyncSpec{TModel}(string)" />.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All errors found, or an empty list when the document would load.</returns>
    public IReadOnlyList<RuleError> ValidateAsyncSpec<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            AsyncRuleBinder.Bind<TModel>(document, _source, _options, errors);
        return errors;
    }

    /// <summary>
    /// Checks a rule document structurally and semantically against the registry for an
    /// asynchronous metadata load, accumulating every error instead of throwing. Parameter values
    /// are not taken: required parameters are stood in by type-shaped placeholders, so supply
    /// errors are only reported by <see cref="DeserializeAsyncSpec{TModel, TMetadata}(string)" />.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to validate.</param>
    /// <returns>All errors found, or an empty list when the document would load.</returns>
    public IReadOnlyList<RuleError> ValidateAsyncSpec<TModel, TMetadata>(string json)
    {
        if (typeof(TMetadata) == typeof(string))
            return ValidateAsyncSpec<TModel>(json);

        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            new AsyncMetadataRuleBinder<TMetadata>(_source, _options).Bind<TModel>(document, errors);
        return errors;
    }

    /// <summary>
    /// Reads the document-level audit flag without binding. Called only after a document has already
    /// bound, so a parse failure is impossible — the same re-parse <c>RuleSet.ReferencesOf</c> does,
    /// for the same reason: these are facts about the envelope, and the binder returns only the tree.
    /// </summary>
    internal bool IsAudited(string json)
    {
        var errors = new List<RuleError>();
        return new RuleDocumentParser(_options).Parse(json, errors)?.Audited ?? false;
    }

    private RuleDocument? Prepare(
        string json,
        IReadOnlyDictionary<string, object?>? parameters,
        List<RuleError> errors)
    {
        var document = new RuleDocumentParser(_options).Parse(json, errors);
        if (document is null)
            return null;

        var values = RuleParameterResolver.Resolve(document.Parameters, parameters, errors);
        Substitute(document, values, errors);
        return document;
    }

    private RuleDocument? PrepareForValidation(string json, List<RuleError> errors)
    {
        var document = new RuleDocumentParser(_options).Parse(json, errors);
        if (document is null)
            return null;

        var values = RuleParameterResolver.ResolveForValidation(document.Parameters);
        Substitute(document, values, errors);
        return document;
    }

    /// <summary>
    /// Substitutes parameter values through the whole document. Definition bodies are walked
    /// alongside the root rather than through the locals that reach them: a definition referenced
    /// twice must be interpolated once, and an unreferenced one still has to report its own errors.
    /// </summary>
    private static void Substitute(
        RuleDocument document,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        if (document.Root is not null)
            RuleParameterSubstituter.Apply(document.Root, values, errors, document.Parameters);

        foreach (var definition in document.Definitions)
            RuleParameterSubstituter.Apply(definition, values, errors, document.Parameters);
    }

    private static void ThrowIfInvalid(List<RuleError> errors)
    {
        if (errors.Count > 0)
            throw new RuleSerializationException(errors);
    }
}
