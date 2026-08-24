namespace Motiv.Tests.Traversal;

/// <summary>
/// Regression cover for the crash Spec 3A removes: reading a result composed far deeper than the
/// thread's stack would allow. Each case runs on an explicitly-sized 1 MB thread — the ASP.NET
/// request-thread stack, and the size at which the ceilings in the design doc were measured. On the
/// 8 MB main stack most of these would pass without the fix and prove nothing.
/// </summary>
/// <remarks>
/// These abort the whole test process rather than failing, so a case stays skipped until the member
/// it covers has been made stack-safe.
/// </remarks>
public class DeepCompositionTests
{
    private const int Operands = 3_000;
    private const int StackBytes = 1024 * 1024;

    /// <summary>Unskipped member by member as Spec 3A makes each one stack-safe.</summary>
    private const string StillRecursive = "Aborts the test process until this member is stack-safe (ticket 19).";

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
    /// On the short-circuiting chain, because the metadata tier over a fully-causal <c>And</c> chain
    /// has a quadratic-plus number of edges — a cost that predates this slice (measured slower before
    /// it than after) and is not this slice's to fix. The tree is just as deep either way, which is
    /// what this case is about.
    /// </remarks>
    [Fact]
    public void Should_read_RootValues_of_a_deep_composition() =>
        OnASmallStack(() => DeepOrElse().RootValues.Count().ShouldBeGreaterThan(0));

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

    [Fact(Skip = StillRecursive)]
    public void Should_read_Justification_of_a_deep_composition() =>
        OnASmallStack(() => DeepAnd().Justification.Length.ShouldBeGreaterThan(0));

    /// <summary>
    /// The uniformity invariant: no public member of a result has a lower depth ceiling than any
    /// other. A single result, read through every member, on one small stack.
    /// </summary>
    [Fact(Skip = StillRecursive)]
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
