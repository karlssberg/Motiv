namespace Motiv.Serialization;

/// <summary>Options that control how rule documents are validated and loaded.</summary>
public sealed class RuleSerializerOptions
{
    /// <summary>The maximum nesting depth a rule document may have. Defaults to 64.</summary>
    public int MaxDocumentDepth { get; set; } = 64;

    /// <summary>The maximum number of rule nodes a document may contain. Defaults to 10,000.</summary>
    public int MaxNodeCount { get; set; } = 10_000;
}
