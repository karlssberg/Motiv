namespace Motiv.Tests.Traversal;

/// <summary>
/// Regression cover for the crash Spec 3A removes: reading a result composed far deeper than the
/// thread's stack would allow. Each case runs on an explicitly-sized 1 MB thread — the ASP.NET
/// request-thread stack, and the size at which the ceilings in the design doc were measured. On the
/// 8 MB main stack most of these would pass without the fix and prove nothing.
/// </summary>
/// <remarks>
/// Before Spec 3A each of these aborted the whole test process rather than failing, so they were
/// written skipped and unskipped one at a time as their member was folded.
/// </remarks>
public class DeepCompositionTests
{
    private const int Operands = 3_000;
    private const int StackBytes = 1024 * 1024;

    [Fact]
    public void Should_read_UnderlyingAssertionSources_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().UnderlyingAssertionSources.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_UnderlyingAllAssertionSources_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().UnderlyingAllAssertionSources.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_UnderlyingMetadataSources_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().UnderlyingMetadataSources.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_UnderlyingExpressionResults_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().UnderlyingExpressionResults.Count().ShouldBe(
            0,
            "a chain of nothing but binary operations has no expression boundary to report — the point " +
            "of the case is that reading it returns rather than aborting"));

    [Fact]
    public void Should_read_UnderlyingReasons_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().UnderlyingReasons.Count().ShouldBe(0, "one reason per expression result"));

    [Fact]
    public void Should_read_AllAssertions_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().AllAssertions.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_Assertions_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().Assertions.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_SubAssertions_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().SubAssertions.Count().ShouldBe(
            0,
            "a composition of atomic propositions has no layer beneath its own assertions — the point " +
            "of the case is that reading it returns rather than aborting"));

    [Fact]
    public void Should_read_AllSubAssertions_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().AllSubAssertions.Count().ShouldBe(0, "as for SubAssertions"));

    [Fact]
    public void Should_read_RootAssertions_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().RootAssertions.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_AllRootAssertions_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().AllRootAssertions.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_Values_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().Values.Count().ShouldBeGreaterThan(0));

    /// <remarks>
    /// On the fully-causal chain like every other case here. Spec 3A had to park this one on the
    /// short-circuiting chain, whose tier has one causal operand per level: over an <c>And</c> chain
    /// the walk cost an extra factor of <c>n</c> and 3,000 operands took minutes. Ticket #137
    /// measured that; ticket #136 removed it, by stopping <c>UnderlyingMetadataSources</c> from
    /// reporting a composition as its own source. What remains is quadratic and shared with
    /// <c>Values</c> — see <see cref="MetadataTierCostTests" />, which holds the cover
    /// for both, and #195 for the remainder.
    /// </remarks>
    [Fact]
    public void Should_read_RootValues_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().RootValues.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_the_underlying_explanations_of_a_deep_composition() =>
        OnASmallStack(() =>
        {
            var result = DeepAnd();
            result.Explanation.Underlying.Count().ShouldBe(0, "as for SubAssertions, which projects it");
            result.Explanation.AllUnderlying.Count().ShouldBe(0, "as for AllSubAssertions");
        });

    [Fact]
    public void Should_read_Reason_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().Reason.Length.ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_Justification_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().Justification.Length.ShouldBeGreaterThan(0));

    /// <summary>
    /// The uniformity invariant: no public member of a result has a lower depth ceiling than any
    /// other. A single result, read through every member, on one small stack.
    /// </summary>
    /// <remarks>
    /// The one case still on the short-circuiting chain, and not for cost — it passes on
    /// <see cref="DeepAnd" /> too. This chain is satisfied at its first operand, so every level of it
    /// is the single-operand <c>OrElse</c> node, a result shape the <c>And</c> chain does not contain
    /// at all. Moving it would trade that coverage away for nothing.
    /// </remarks>
    [Fact]
    public void Should_read_every_member_of_one_deep_result() =>
        OnASmallStack(() =>
        {
            var result = DeepOrElse();

            _ = result.Satisfied;
            _ = result.Reason;
            _ = result.Justification;
            _ = result.Assertions.Count();
            _ = result.AllAssertions.Count();
            _ = result.SubAssertions.Count();
            _ = result.AllSubAssertions.Count();
            _ = result.RootAssertions.Count();
            _ = result.AllRootAssertions.Count();
            _ = result.UnderlyingAssertionSources.Count();
            _ = result.UnderlyingAllAssertionSources.Count();
            _ = result.UnderlyingExpressionResults.Count();
            _ = result.UnderlyingReasons.Count();
            _ = result.UnderlyingMetadataSources.Count();
            _ = result.Values.Count();
            _ = result.RootValues.Count();
            _ = result.Explanation.Underlying.Count();
            _ = result.Explanation.AllUnderlying.Count();
            _ = result.Description.Reason;
            _ = result.Description.Justification;
        });

    /// <summary>
    /// A left-deep <c>And</c> chain in which every operand is causal — the shape
    /// <c>specs.Aggregate((a, b) =&gt; a.And(b))</c> produces, and the one the design doc's ceilings
    /// were measured on. Composed fresh per test: a shared instance would let one test's memoised
    /// walk keep the next test's from ever recursing, so a still-recursive member could pass on the
    /// strength of its neighbours.
    /// </summary>
    private static BooleanResultBase<string> DeepAnd() =>
        Chain((left, right) => left.And(right)).Evaluate(2);

    /// <summary>
    /// A left-deep short-circuiting <c>OrElse</c> chain: equally deep, but only one causal operand
    /// per level, so the metadata tier beneath it has a linear rather than quadratic number of edges.
    /// </summary>
    private static BooleanResultBase<string> DeepOrElse() =>
        Chain((left, right) => left.OrElse(right)).Evaluate(2);

    private static SpecBase<int, string> Chain(
        Func<SpecBase<int, string>, SpecBase<int, string>, SpecBase<int, string>> combine) =>
        Enumerable
            .Range(0, Operands)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"))
            .Aggregate(combine);

    private static void OnASmallStack(Action body)
    {
        Exception? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    body();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            },
            StackBytes);

        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
