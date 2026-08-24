using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>Options that control how rule documents are validated and loaded.</summary>
public sealed class RuleSerializerOptions
{
    private int _maxDocumentDepth = 64;
    private int _maxNodeCount = 10_000;
    private int _maxCompositionDepth = 256;

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
    /// The maximum depth of the <em>composed</em> spec a document may bind to. Defaults to 256.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distinct from <see cref="MaxDocumentDepth" />, which counts JSON nesting. An n-ary operator
    /// folds left-deep into n-1 binary compositions, so a single shallow node may compose far deeper
    /// than the document nests, and nesting multiplies rather than adds.
    /// </para>
    /// <para>
    /// This cap was originally derived against stack use — result-tree walks recursed over the
    /// composed shape at roughly a kilobyte per level. Those walks are iterative as of Spec 3A, so
    /// the cap now bounds the <em>size</em> of the result tree and the work of building it, not the
    /// stack. The default is unchanged pending the re-derivation, because evaluating the composition
    /// is itself still recursive and is now what the cap has to stay beneath.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxCompositionDepth
    {
        get => _maxCompositionDepth;
        set => _maxCompositionDepth = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                "MaxCompositionDepth must be at least 1.");
    }

    /// <summary>
    /// The <see cref="JsonSerializerOptions" /> used to deserialize object 'whenTrue'/'whenFalse'
    /// payloads into the metadata type of a metadata load. <c>null</c> uses System.Text.Json defaults.
    /// </summary>
    public JsonSerializerOptions? MetadataJsonOptions { get; set; }
}
