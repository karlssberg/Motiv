namespace Motiv.Serialization;

/// <summary>
/// Loads externalized JSON rule documents into Motiv specs, resolving leaf references against a
/// <see cref="SpecRegistry" />.
/// </summary>
public sealed class RuleSerializer
{
    private readonly SpecRegistry _registry;
    private readonly RuleSerializerOptions _options;

    /// <summary>Creates a serializer that resolves spec references against the given registry.</summary>
    /// <param name="registry">The registry used to resolve spec references.</param>
    /// <param name="options">Options controlling validation and loading; defaults are used when omitted.</param>
    public RuleSerializer(SpecRegistry registry, RuleSerializerOptions? options = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
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

        var spec = RuleBinder.Bind<TModel>(document!, _registry, errors);
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

        var spec = AsyncRuleBinder.Bind<TModel>(document!, _registry, errors);
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

        var spec = new MetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document!, errors);
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

        var spec = new AsyncMetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document!, errors);
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
            RuleBinder.Bind<TModel>(document, _registry, errors);
        return errors;
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
            new MetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document, errors);
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
            AsyncRuleBinder.Bind<TModel>(document, _registry, errors);
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
            new AsyncMetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document, errors);
        return errors;
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
        if (document.Root is not null)
            RuleParameterSubstituter.Apply(document.Root, values, errors);
        return document;
    }

    private RuleDocument? PrepareForValidation(string json, List<RuleError> errors)
    {
        var document = new RuleDocumentParser(_options).Parse(json, errors);
        if (document is null)
            return null;

        var values = RuleParameterResolver.ResolveForValidation(document.Parameters);
        if (document.Root is not null)
            RuleParameterSubstituter.Apply(document.Root, values, errors);
        return document;
    }

    private static void ThrowIfInvalid(List<RuleError> errors)
    {
        if (errors.Count > 0)
            throw new RuleSerializationException(errors);
    }
}
