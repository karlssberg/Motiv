namespace Motiv.Serialization;

/// <summary>
/// A rule's default implementation: either a compiled spec (bound at construction, no document)
/// or a serialized rule document (bound later, when the rule is added to a <see cref="RuleSet"/>).
/// </summary>
internal sealed class RuleDefault
{
    private RuleDefault(object? compiledSpec, string? documentJson)
    {
        CompiledSpec = compiledSpec;
        DocumentJson = documentJson;
    }

    /// <summary>The compiled default spec/policy, or null for a document default.</summary>
    public object? CompiledSpec { get; }

    /// <summary>The document-default JSON, or null for a compiled default.</summary>
    public string? DocumentJson { get; }

    public static RuleDefault Compiled(object spec) => new(spec, null);

    public static RuleDefault Document(string json) => new(null, json);
}
