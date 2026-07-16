using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>Options that control how rule documents are validated and loaded.</summary>
public sealed class RuleSerializerOptions
{
    private int _maxDocumentDepth = 64;
    private int _maxNodeCount = 10_000;

    /// <summary>The maximum nesting depth a rule document may have. Defaults to 64.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxDocumentDepth
    {
        get => _maxDocumentDepth;
        set => _maxDocumentDepth = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "MaxDocumentDepth must be at least 1.");
    }

    /// <summary>The maximum number of rule nodes a document may contain. Defaults to 10,000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxNodeCount
    {
        get => _maxNodeCount;
        set => _maxNodeCount = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "MaxNodeCount must be at least 1.");
    }

    /// <summary>
    /// The <see cref="JsonSerializerOptions" /> used to deserialize object 'whenTrue'/'whenFalse'
    /// payloads into the metadata type of a metadata load. <c>null</c> uses System.Text.Json defaults.
    /// </summary>
    public JsonSerializerOptions? MetadataJsonOptions { get; set; }
}
