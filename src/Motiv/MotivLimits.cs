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
    /// <para>
    /// That exclusion is <em>declared</em> rather than detected, and the distinction matters when you
    /// write the node. The engine cannot tell a re-entrant evaluation that is part of the composition
    /// from one that is work inside a node, so the library marks the places it knows: resolving an
    /// element of a higher-order proposition, <c>EnumerableExtensions.Where</c>, and a <c>Tap</c>
    /// callback. Everything else that evaluates a proposition while an evaluation is in flight
    /// <b>is</b> counted — notably a predicate of your own that evaluates a proposition per item
    /// (<c>Spec.Build((Order o) =&gt; o.Lines.All(line.Matches))</c>), a higher-order predicate supplied
    /// through <c>As(...)</c>, and a <c>WhenTrue</c>/<c>WhenFalse</c> delegate resolved while another
    /// evaluation is running. Prefer the built-in quantifiers — <c>AsAllSatisfied</c> and its siblings —
    /// where the per-item work should not count against the rule that contains it.
    /// </para>
    /// <para>
    /// Work spread across <em>decorator layers</em> is counted, though. A decorator between two operator
    /// layers is not folded — it re-enters the fold — but the nested fold spends the same budget, so
    /// fifty layers of ten operands is refused by the same limit of 100 that refuses the flat chain of
    /// 200. It was not, until
    /// <see href="https://github.com/karlssberg/Motiv/issues/202">#202</see>: the count lived in a
    /// fold-local, and the shape a rule document composes is exactly the alternating one.
    /// </para>
    /// <para>
    /// One asymmetry remains. <see cref="AsyncSpecBase{TModel}.EvaluateAsync" /> and
    /// <see cref="AsyncSpecBase{TModel}.MatchesAsync" /> still count per fold, because the budget is a
    /// thread-static — correct for the synchronous folds, which never leave the thread that started
    /// them, and unavailable to an asynchronous one whose continuation may resume elsewhere. Tracked as
    /// <see href="https://github.com/karlssberg/Motiv/issues/204">#204</see>.
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
