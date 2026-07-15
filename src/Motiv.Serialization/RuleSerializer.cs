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
}
