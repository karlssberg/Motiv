namespace Motiv.Tests;

/// <summary>
/// Pins that a first-order result built from <c>WhenFalseYield</c> runs the caller's resolver at most once,
/// however many of its consumers are read. Each result memoises the resolved sequence; without the memo, every
/// read of <c>Values</c>, <c>Assertions</c> or <c>Justification</c> would run the caller's code again, and a
/// non-deterministic resolver would leave those properties disagreeing within one result (#294).
/// <para>
/// Every proposition here is built through the public builder and evaluated against a model that takes the
/// <c>WhenFalseYield</c> branch, so the counted delegate is the one the result resolves.
/// </para>
/// </summary>
public class YieldResolverMemoisationTests
{
    private const int NotPositive = -1;

    private static SpecBase<int, string> UnderlyingSpec => Spec.Build((int n) => n > 0).Create("is positive");

    private static PolicyBase<int, string> UnderlyingPolicy => Spec.Build((int n) => n > 0).Create("is positive");

    private static readonly (string ResultType, Func<CallCounter, SpecBase<int, string>> Build)[] WhenFalseYieldPaths =
    [
        (
            "MultiAssertionExplanationPropositionBooleanResult",
            counter => Spec.Build((int n) => n > 0)
                .WhenTrue("is positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create()),
        (
            "ExpressionTreeMultiAssertionExplanationPropositionBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create()),
        (
            "ExpressionTreeMultiMetadataPropositionBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create("positive")),
        (
            "BooleanResultPredicateMultiAssertionExplanationBooleanResult",
            counter => Spec.Build((int n) => UnderlyingSpec.Evaluate(n))
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create()),
        (
            "BooleanResultPredicateMultiValueBooleanResult",
            counter => Spec.Build((int n) => UnderlyingSpec.Evaluate(n))
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create("positive")),
        (
            "PolicyResultPredicateMultiAssertionExplanationBooleanResult",
            counter => Spec.Build((int n) => UnderlyingPolicy.Evaluate(n))
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create()),
        (
            "PolicyResultPredicateMultiValueBooleanResult",
            counter => Spec.Build((int n) => UnderlyingPolicy.Evaluate(n))
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create("positive")),
        (
            "SpecDecoratorMultiMetadataBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create("positive")),
        (
            "PolicyDecoratorMultiMetadataBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create("positive")),
        (
            "SpecDecoratorMultiAssertionExplanationBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create()),
        (
            "PolicyDecoratorMultiAssertionExplanationBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .WhenTrue("is positive")
                .WhenFalseYield((_, _) => counter.Pass(new[] { "is not positive", "is zero or less" }))
                .Create())
    ];

    public static TheoryData<string> WhenFalseYieldResultTypes =>
        WhenFalseYieldPaths.Select(path => path.ResultType).ToTheoryData();

    [Theory]
    [MemberData(nameof(WhenFalseYieldResultTypes))]
    public void WhenFalseYieldResolver_IsInvokedOnceAcrossRepeatedReads(string resultType)
    {
        var build = WhenFalseYieldPaths.Single(path => path.ResultType == resultType).Build;
        var counter = new CallCounter();
        var result = build(counter).Evaluate(NotPositive);

        result.GetType().NameWithoutArity().ShouldBe(resultType);
        ReadEveryConsumerRepeatedly(result);

        result.Satisfied.ShouldBeFalse(resultType);
        counter.Calls.ShouldBe(1, resultType);
    }

    private static void ReadEveryConsumerRepeatedly(BooleanResultBase<string> result)
    {
        for (var read = 0; read < 3; read++)
        {
            _ = result.MetadataTier.Metadata.ToArray();
            _ = result.Values.ToArray();
            _ = result.Explanation.Assertions.ToArray();
            _ = result.Assertions.ToArray();
            _ = result.Reason;
            _ = result.Justification;
        }
    }
}
