using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>Options that control how rule documents are validated and loaded.</summary>
public sealed class RuleSerializerOptions
{
    private int _maxDocumentDepth = 64;
    private int _maxNodeCount = 10_000;
    private int _maxCompositionDepth = 4_096;

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
    /// The maximum depth of the <em>composed</em> spec a document may bind to. Defaults to 4,096.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distinct from <see cref="MaxDocumentDepth" />, which counts JSON nesting. An n-ary operator
    /// folds left-deep into n-1 binary compositions, so a single shallow node may compose far deeper
    /// than the document nests, and nesting multiplies rather than adds.
    /// </para>
    /// <para>
    /// This cap was originally derived against stack use — result-tree walks recursed over the
    /// composed shape at roughly a kilobyte per level, which is where the old default of 256 came
    /// from. Neither those walks (Spec 3A) nor evaluation itself, synchronous or asynchronous
    /// (Spec 3E), recurses any more, so stack is no longer the constraint and the number is
    /// re-derived against <em>cost per evaluation</em>: a composition this deep evaluates in about
    /// 2.7 ms synchronously and 1.8 ms asynchronously, retaining about a megabyte of result, where
    /// 16,384 costs 7–9 ms. That is the budget a document may demand of every request that evaluates
    /// the rule it binds to.
    /// </para>
    /// <para>
    /// Async is what held the old number down and did not say so: before Spec 3E an async composition
    /// aborted the process at 633 operands, so 256 was safe only by accident. Raising this cap and
    /// making async evaluation iterative were the same decision.
    /// </para>
    /// <para>
    /// It sits deliberately below <see cref="MaxNodeCount" />'s implicit ceiling — a 10,000-node
    /// document cannot compose much beyond 10,000 deep — so the two caps stay independently
    /// meaningful, and below <c>MotivLimits.MaxEvaluationSize</c>, which is the engine's backstop for
    /// compositions that never came from a document at all.
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
