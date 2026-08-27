namespace Motiv;

/// <summary>
/// Process-wide limits on how much work a single evaluation may do. Set them once at startup, as with
/// <see cref="Diagnostics.MotivTelemetry.ExplanationDetail" />.
/// </summary>
public static class MotivLimits
{
    /// <summary>The default value of <see cref="MaxEvaluationSize" />.</summary>
    /// <remarks>
    /// Derived rather than chosen: a node of the thinnest composition there is costs about 190 bytes of
    /// retained result (measured over left-deep <c>And</c> chains of 1,000 to 100,000 propositions), so a
    /// quarter of a million of them puts a single evaluation's ceiling near 50 MB. That is far above any
    /// composition an author writes — 250,000 nodes is a chain of 125,000 propositions — and far below
    /// what a request body should be able to spend.
    /// </remarks>
    public const int DefaultMaxEvaluationSize = 250_000;

    private static int _maxEvaluationSize = DefaultMaxEvaluationSize;

    /// <summary>
    /// The maximum number of nodes a single evaluation may compose before it is abandoned with a
    /// <see cref="SpecException" /> — one node per proposition evaluated, and one per logical operation
    /// joining them, so a chain of <c>n</c> propositions is <c>2n - 1</c> nodes.
    /// </summary>
    /// <remarks>
    /// Applies to <see cref="SpecBase{TModel}.Evaluate" /> and <see cref="SpecBase{TModel}.Matches" />
    /// alike — <c>Matches</c> materialises no results, but it walks the same tree, and a composition one
    /// entry point accepts should never be one the other refuses.
    /// <para>
    /// This is a backstop in the engine, not a validator. A host that binds rule documents should refuse
    /// an oversized document at its edge — <c>RuleSerializerOptions.MaxCompositionDepth</c> does that,
    /// and its message can name the document where this one can only name a count.
    /// </para>
    /// <para>
    /// It counts the nodes of the logical composition the evaluation folds, which is the quantity a flat
    /// operand array controls. Work done <em>inside</em> a node — a higher-order proposition over a large
    /// collection, say — is not counted and is not bounded by this.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
    public static int MaxEvaluationSize
    {
        get => _maxEvaluationSize;
        set => _maxEvaluationSize = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value,
                $"{nameof(MaxEvaluationSize)} must be at least 1.");
    }
}
