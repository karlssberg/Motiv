namespace Motiv.Tests.HigherOrderProposition;

/// <summary>
/// Pins that a higher-order result runs each of the caller's delegates at most once, however many of its
/// consumers are read — the cause selector passed to <c>As(predicate, causeSelector)</c>, and the
/// <c>WhenFalseYield</c> resolver. Each result memoises what those delegates return; without the memo, every
/// read of <c>Causes</c>, <c>Values</c>, <c>Assertions</c> or <c>Justification</c> would run the caller's code
/// again, and a non-deterministic delegate would leave those properties disagreeing within one result (#294).
/// <para>
/// Every proposition here is built through the public builder, so each row reaches the result class it names
/// only by the path a caller would take.
/// </para>
/// </summary>
public class HigherOrderUserDelegateMemoisationTests
{
    private static readonly int[] AllPositive = [1, 2, 3];
    private static readonly int[] SomeNegative = [1, -2, 3];

    private static SpecBase<int, string> UnderlyingSpec => Spec.Build((int n) => n > 0).Create("is positive");

    private static PolicyBase<int, string> UnderlyingPolicy => Spec.Build((int n) => n > 0).Create("is positive");

    private static readonly (string ResultType, Func<CallCounter, SpecBase<IEnumerable<int>, string>> Build)[]
        CauseSelectorPaths =
    [
        // ---------- BooleanPredicate family: Spec.Build((int n) => n > 0) ----------
        (
            "HigherOrderFromBooleanPredicateExplanationPolicyResult",
            counter => Spec.Build((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create()),
        (
            "HigherOrderFromBooleanPredicateMetadataPolicyResult",
            counter => Spec.Build((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create("all are positive")),
        (
            "HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult",
            counter => Spec.Build((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create()),
        (
            "HigherOrderFromBooleanPredicateMultiMetadataBooleanResult",
            counter => Spec.Build((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create("all are positive")),

        // ---------- BooleanResultPredicate family: Spec.Build(SpecBase<int, string>) ----------
        (
            "HigherOrderFromBooleanResultExplanationPolicyResult",
            counter => Spec.Build(UnderlyingSpec)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create()),
        (
            "HigherOrderFromBooleanResultPolicyResult",
            counter => Spec.Build(UnderlyingSpec)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create("all are positive")),
        (
            "HigherOrderFromBooleanResultMultiAssertionExplanationBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create()),
        (
            "HigherOrderFromBooleanResultMultiMetadataBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create("all are positive")),
        (
            "MinimalHigherOrderFromBooleanResultBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .Create("all are positive")),

        // ---------- PolicyResultPredicate family: Spec.Build(PolicyBase<int, string>) ----------
        (
            "HigherOrderFromPolicyResultExplanationPolicyResult",
            counter => Spec.Build(UnderlyingPolicy)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create()),
        (
            "HigherOrderFromPolicyResultMetadataPolicyResult",
            counter => Spec.Build(UnderlyingPolicy)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create("all are positive")),
        (
            "HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create()),
        (
            "HigherOrderFromPolicyResultMultiMetadataBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create("all are positive")),
        (
            "MinimalHigherOrderFromPolicyResultBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .Create("all are positive")),

        // ---------- ExpressionTree family: Spec.From((int n) => n > 0) ----------
        (
            "HigherOrderFromExpressionTreeExplanationPolicyResult",
            counter => Spec.From((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create()),
        (
            "HigherOrderFromExpressionTreeMetadataPolicyResult",
            counter => Spec.From((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalse("not all positive")
                .Create("all are positive")),
        (
            "HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create()),
        (
            "HigherOrderFromExpressionTreeMultiMetadataBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .WhenTrue("all positive")
                .WhenFalseYield(_ => ["not all positive"])
                .Create("all are positive")),
        (
            "MinimalHigherOrderFromExpressionTreeBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .As(results => results.All(r => r.Satisfied), (_, results) => counter.Pass(results))
                .Create("all are positive"))
    ];

    private static readonly (string ResultType, Func<CallCounter, SpecBase<IEnumerable<int>, string>> Build)[]
        YieldResolverPaths =
    [
        (
            "HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult",
            counter => Spec.Build((int n) => n > 0)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create()),
        (
            "HigherOrderFromBooleanPredicateMultiMetadataBooleanResult",
            counter => Spec.Build((int n) => n > 0)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create("all are positive")),
        (
            "HigherOrderFromBooleanResultMultiAssertionExplanationBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create()),
        (
            "HigherOrderFromBooleanResultMultiMetadataBooleanResult",
            counter => Spec.Build(UnderlyingSpec)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create("all are positive")),
        (
            "HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create()),
        (
            "HigherOrderFromPolicyResultMultiMetadataBooleanResult",
            counter => Spec.Build(UnderlyingPolicy)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create("all are positive")),
        (
            "HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create()),
        (
            "HigherOrderFromExpressionTreeMultiMetadataBooleanResult",
            counter => Spec.From((int n) => n > 0)
                .AsAllSatisfied()
                .WhenTrue("all positive")
                .WhenFalseYield(_ => counter.Pass(new[] { "not all positive", "some are negative" }))
                .Create("all are positive"))
    ];

    public static TheoryData<string> CauseSelectorResultTypes =>
        CauseSelectorPaths.Select(path => path.ResultType).ToTheoryData();

    public static TheoryData<string> YieldResolverResultTypes =>
        YieldResolverPaths.Select(path => path.ResultType).ToTheoryData();

    [Theory]
    [MemberData(nameof(CauseSelectorResultTypes))]
    public void CauseSelector_IsInvokedOnceAcrossRepeatedReads(string resultType)
    {
        var build = CauseSelectorPaths.Single(path => path.ResultType == resultType).Build;
        foreach (var models in new[] { AllPositive, SomeNegative })
        {
            var counter = new CallCounter();
            var result = build(counter).Evaluate(models);

            result.GetType().NameWithoutArity().ShouldBe(resultType);
            ReadEveryConsumerRepeatedly(result);

            counter.Calls.ShouldBe(1, $"{resultType} over [{string.Join(", ", models)}]");
        }
    }

    [Theory]
    [MemberData(nameof(YieldResolverResultTypes))]
    public void WhenFalseYieldResolver_IsInvokedOnceAcrossRepeatedReads(string resultType)
    {
        var build = YieldResolverPaths.Single(path => path.ResultType == resultType).Build;
        var counter = new CallCounter();
        var result = build(counter).Evaluate(SomeNegative);

        result.GetType().NameWithoutArity().ShouldBe(resultType);
        ReadEveryConsumerRepeatedly(result);

        result.Satisfied.ShouldBeFalse(resultType);
        counter.Calls.ShouldBe(1, resultType);
    }

    private static void ReadEveryConsumerRepeatedly(BooleanResultBase<string> result)
    {
        for (var read = 0; read < 3; read++)
        {
            _ = result.Causes.ToArray();
            _ = result.CausesWithValues.ToArray();
            _ = result.MetadataTier.Metadata.ToArray();
            _ = result.Values.ToArray();
            _ = result.Explanation.Assertions.ToArray();
            _ = result.Assertions.ToArray();
            _ = result.Reason;
            _ = result.Justification;
        }
    }
}
