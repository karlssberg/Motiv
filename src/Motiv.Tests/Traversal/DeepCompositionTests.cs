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

    private static readonly Lazy<BooleanResultBase<string>> DeepResult = new(Compose);

    [Fact]
    public void Should_read_UnderlyingAssertionSources_of_a_deep_composition() =>
        OnASmallStack(() => Deep().UnderlyingAssertionSources.Count().ShouldBeGreaterThan(0));

    [Fact]
    public void Should_read_UnderlyingAllAssertionSources_of_a_deep_composition() =>
        OnASmallStack(() => Deep().UnderlyingAllAssertionSources.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_UnderlyingMetadataSources_of_a_deep_composition() =>
        OnASmallStack(() => Deep().UnderlyingMetadataSources.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_UnderlyingExpressionResults_of_a_deep_composition() =>
        OnASmallStack(() => Deep().UnderlyingExpressionResults.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_UnderlyingReasons_of_a_deep_composition() =>
        OnASmallStack(() => Deep().UnderlyingReasons.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_AllAssertions_of_a_deep_composition() =>
        OnASmallStack(() => Deep().AllAssertions.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_Assertions_of_a_deep_composition() =>
        OnASmallStack(() => Deep().Assertions.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_SubAssertions_of_a_deep_composition() =>
        OnASmallStack(() => Deep().SubAssertions.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_AllSubAssertions_of_a_deep_composition() =>
        OnASmallStack(() => Deep().AllSubAssertions.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_RootAssertions_of_a_deep_composition() =>
        OnASmallStack(() => Deep().RootAssertions.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_AllRootAssertions_of_a_deep_composition() =>
        OnASmallStack(() => Deep().AllRootAssertions.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_Values_of_a_deep_composition() =>
        OnASmallStack(() => Deep().Values.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_RootValues_of_a_deep_composition() =>
        OnASmallStack(() => Deep().RootValues.Count().ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_the_underlying_explanations_of_a_deep_composition() =>
        OnASmallStack(() =>
        {
            var result = Deep();
            result.Explanation.Underlying.Count().ShouldBeGreaterThan(0);
            result.Explanation.AllUnderlying.Count().ShouldBeGreaterThan(0);
        });

    [Fact(Skip = StillRecursive)]
    public void Should_read_Reason_of_a_deep_composition() =>
        OnASmallStack(() => Deep().Reason.Length.ShouldBeGreaterThan(0));

    [Fact(Skip = StillRecursive)]
    public void Should_read_Justification_of_a_deep_composition() =>
        OnASmallStack(() => Deep().Justification.Length.ShouldBeGreaterThan(0));

    /// <summary>
    /// The uniformity invariant: no public member of a result has a lower depth ceiling than any
    /// other. A single result, read through every member, on one small stack.
    /// </summary>
    [Fact(Skip = StillRecursive)]
    public void Should_read_every_member_of_one_deep_result() =>
        OnASmallStack(() =>
        {
            var result = Deep();

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

    private static BooleanResultBase<string> Deep() => DeepResult.Value;

    private static BooleanResultBase<string> Compose()
    {
        var specs = Enumerable
            .Range(0, Operands)
            .Select(i => (SpecBase<int, string>)Spec.Build((int n) => n % 2 == 0).Create($"p{i} is even"))
            .ToArray();

        return specs.Aggregate((left, right) => left.And(right)).Evaluate(2);
    }

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
