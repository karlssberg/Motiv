namespace Motiv.Tests.Traversal;

/// <summary>
/// Cover for ticket #139 — the cost the decision log put on the evaluation path. The ticket suspected
/// the metadata tier, on the grounds that <c>RuleEvaluationResult</c> reads <c>Values</c> and #137
/// recorded that tier as quadratic-plus. Measuring the projection's six reads separately settled it
/// against that: <c>Values</c> and <c>Justification</c> are both linear, and the dominant term is
/// <see cref="BooleanResultBase.Reason" />, which grew as <c>n^1.88</c> — 51.8 MB allocated to build a
/// 32 KB string over a 1,600-operand chain.
/// </summary>
/// <remarks>
/// <para>
/// The cause is an asymmetry between the two renderers of the same description tree.
/// <c>Justification</c> builds its lines from a <i>collapsed</i> operand list, so a run of nested
/// same-operation compositions becomes one heading over the whole run and the fold never visits the
/// intervening levels. <c>Reason</c> composed from the two direct causal operands instead, so its
/// fold visited every level of the run and materialised a string of length O(k) at level k — and
/// memoised each one, making the square retained rather than merely transient.
/// </para>
/// <para>
/// The flattening is only sound where the pass-through it replaces was an identity, which is what the
/// two guards below pin. Both were reached by hand before the change and both go red against a
/// flatten that omits their condition — see the design doc for the transcripts.
/// </para>
/// </remarks>
public class ReasonCostTests
{
#if !NETFRAMEWORK
    /// <summary>The chain lengths the growth is read across, and the factor between them.</summary>
    private const int Small = 200;

    private const int Large = 800;

    private const double SizeFactor = (double)Large / Small;

    /// <summary>
    /// The ratio an exponent of <c>1.5</c> predicts across those two lengths — halfway between linear
    /// growth (<c>4x</c> for a four-fold chain) and quadratic (<c>16x</c>) in log space, so neither
    /// reading is near it. Pre-change the ratio is ~14.9; after it, ~4.
    /// </summary>
    private static readonly double Superlinear = Math.Pow(SizeFactor, 1.5);

    /// <summary>
    /// The cost, in the only form a test can state without a clock — the same form
    /// <see cref="MetadataTierCostTests" /> states its own in. Allocation rather than time because it
    /// is what the defect actually produces and it does not vary with the machine: the intermediate
    /// reason of a chain is a string, so a walk that materialises every level allocates the square of
    /// the chain in characters whatever the clock says.
    /// </summary>
    /// <remarks>
    /// Not built for <c>net472</c>, which has no per-thread allocation counter — only
    /// <c>AppDomain.MonitoringTotalAllocatedMemorySize</c>, which counts every thread in the domain
    /// and so measures whatever else xunit is running in parallel rather than this chain. The defect
    /// is not framework-specific, so the three renderings below carry the guard on all four targets
    /// and this one holds the cost claim on the three that can state it.
    /// </remarks>
    [Fact]
    public void Should_build_a_chains_reason_in_allocation_linear_in_the_chain()
    {
        _ = AllocatedReadingReason(Small); // warm: the first read JITs the fold and its delegates

        var small = AllocatedReadingReason(Small);
        var large = AllocatedReadingReason(Large);
        var growth = (double)large / small;

        growth.ShouldBeLessThan(
            Superlinear,
            $"a {SizeFactor}x longer chain should cost about {SizeFactor}x as much to render, not " +
            $"{SizeFactor * SizeFactor}x — the root's reason is linear in the chain, so an " +
            $"allocation that grows with its square is the intervening levels being materialised and " +
            $"kept rather than the answer being produced " +
            $"({small:N0} bytes at {Small} operands, {large:N0} at {Large})");
    }

#endif

    /// <summary>
    /// The first guard on the flattening. <c>And</c>'s same-family test admits <c>AndAlso</c>, whose
    /// reason is joined with <c>" &amp;&amp; "</c> rather than <c>" &amp; "</c>, so a run may only be
    /// collapsed across one <i>operation</i> — which is the condition the justification's own collapse
    /// already applies, and the reason this change can share it rather than invent one.
    /// </summary>
    [Fact]
    public void Should_not_collapse_a_short_circuiting_operand_into_its_eager_parent()
    {
        var result = Even("a").AndAlso(Even("b")).And(Even("c")).Evaluate(2);

        result.Reason.ShouldBe(
            "(a == true) && (b == true) & (c == true)",
            "the inner composition is an AndAlso: its operands are joined with && and stay joined " +
            "with && when its parent reads them, so the run the parent may collapse stops at it");
    }

    /// <summary>
    /// The second guard, and the one a flatten is likely to miss. A same-operation operand that
    /// contributed a <i>single</i> cause renders as that cause's reason verbatim — no separator to
    /// join and, in particular, no parentheses. Collapsing it would hand its cause to the parent,
    /// which parenthesises an equality assertion, so the run may only be collapsed through an operand
    /// that actually joined two or more.
    /// </summary>
    [Fact]
    public void Should_not_collapse_an_operand_that_contributed_a_single_cause()
    {
        // x is false and y is true, so only x caused the inner And; c is false and causes the outer.
        var result = Odd("x").And(Even("y")).And(Odd("c")).Evaluate(2);

        result.Reason.ShouldBe(
            "x == false & (c == false)",
            "the inner And has one cause, so its reason is that cause's own and carries no " +
            "parentheses; collapsing it would promote the cause to an operand of the outer And, " +
            "which parenthesises an equality assertion, and silently rewrite the rendering");
    }

    /// <summary>
    /// The run is collapsed to its full depth rather than one level, which is what makes the growth
    /// linear rather than merely smaller. Asserted through the rendering, since the operand list the
    /// fold walks is private to the description.
    /// </summary>
    [Fact]
    public void Should_render_a_whole_run_as_one_join_however_deep_it_is()
    {
        var result = Chain(6);

        result.Reason.ShouldBe(
            string.Join(" & ", Enumerable.Range(0, 6).Select(i => $"(p{i} == true)")),
            "a left-nested run of six Ands is one conjunction of six operands, so it renders as one " +
            "join of six — the same run the justification renders under one AND heading");
    }

#if !NETFRAMEWORK
    private static long AllocatedReadingReason(int operands)
    {
        var result = Chain(operands);

        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = result.Reason;

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

#endif

    private static BooleanResultBase<string> Chain(int operands) =>
        Enumerable
            .Range(0, operands)
            .Select(i => Even($"p{i}"))
            .Aggregate((left, right) => left.And(right))
            .Evaluate(2);

    private static SpecBase<int, string> Even(string name) =>
        Spec.Build((int n) => n % 2 == 0).Create(name);

    private static SpecBase<int, string> Odd(string name) =>
        Spec.Build((int n) => n % 2 == 1).Create(name);
}
