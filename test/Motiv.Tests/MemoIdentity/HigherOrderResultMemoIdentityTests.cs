using static Motiv.Tests.MemoIdentity.MemoisedResultCase;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The higher-order results — over a boolean predicate, a spec, a policy and an expression tree — keep
/// their memoised description, explanation and metadata tier across reads.
/// </summary>
public class HigherOrderResultMemoIdentityTests
{
    private static readonly int[] Models = [2, 4, 5];

    private static readonly SpecBase<int, string> IsEvenSpec = Spec.Build((int n) => n % 2 == 0).Create("is even");

    private static readonly PolicyBase<int, string> IsEvenPolicy = Spec.Build((int n) => n % 2 == 0).Create("is even");

    private static readonly MemoisedResultCase[] Cases =
    [
        // Over a boolean predicate.
        Of<string>("minimal boolean-predicate higher order", "HigherOrderFromBooleanPredicateMetadataPolicyResult",
            () => Spec.Build((int n) => n % 2 == 0).AsAllSatisfied().Create("all even").Evaluate(Models)),
        Of<string>("unnamed boolean-predicate higher order", "HigherOrderFromBooleanPredicateExplanationPolicyResult",
            () => Spec.Build((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create()
                .Evaluate(Models)),
        Of<string>("named boolean-predicate higher order", "HigherOrderFromBooleanPredicateMetadataPolicyResult",
            () => Spec.Build((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create("all even")
                .Evaluate(Models)),
        Of<string>("unnamed multi-assertion boolean-predicate higher order", "HigherOrderFromBooleanPredicateMultiAssertionExplanationBooleanResult",
            () => Spec.Build((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create()
                .Evaluate(Models)),
        Of<string>("named multi-assertion boolean-predicate higher order", "HigherOrderFromBooleanPredicateMultiMetadataBooleanResult",
            () => Spec.Build((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create("all even")
                .Evaluate(Models)),

        // Over a spec.
        Of<string>("minimal spec higher order", "MinimalHigherOrderFromBooleanResultBooleanResult",
            () => Spec.Build(IsEvenSpec).AsAllSatisfied().Create("all even").Evaluate(Models)),
        Of<string>("unnamed spec higher order", "HigherOrderFromBooleanResultExplanationPolicyResult",
            () => Spec.Build(IsEvenSpec)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create()
                .Evaluate(Models)),
        Of<string>("named spec higher order", "HigherOrderFromBooleanResultPolicyResult",
            () => Spec.Build(IsEvenSpec)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create("all even")
                .Evaluate(Models)),
        Of<string>("unnamed multi-assertion spec higher order", "HigherOrderFromBooleanResultMultiAssertionExplanationBooleanResult",
            () => Spec.Build(IsEvenSpec)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create()
                .Evaluate(Models)),
        Of<string>("named multi-assertion spec higher order", "HigherOrderFromBooleanResultMultiMetadataBooleanResult",
            () => Spec.Build(IsEvenSpec)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create("all even")
                .Evaluate(Models)),

        // Over a policy.
        Of<string>("minimal policy higher order", "MinimalHigherOrderFromPolicyResultBooleanResult",
            () => Spec.Build(IsEvenPolicy).AsAllSatisfied().Create("all even").Evaluate(Models)),
        Of<string>("unnamed policy higher order", "HigherOrderFromPolicyResultExplanationPolicyResult",
            () => Spec.Build(IsEvenPolicy)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create()
                .Evaluate(Models)),
        Of<string>("named policy higher order", "HigherOrderFromPolicyResultMetadataPolicyResult",
            () => Spec.Build(IsEvenPolicy)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create("all even")
                .Evaluate(Models)),
        Of<string>("unnamed multi-assertion policy higher order", "HigherOrderFromPolicyResultMultiAssertionExplanationBooleanResult",
            () => Spec.Build(IsEvenPolicy)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create()
                .Evaluate(Models)),
        Of<string>("named multi-assertion policy higher order", "HigherOrderFromPolicyResultMultiMetadataBooleanResult",
            () => Spec.Build(IsEvenPolicy)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create("all even")
                .Evaluate(Models)),

        // Over an expression tree.
        Of<string>("minimal expression-tree higher order", "MinimalHigherOrderFromExpressionTreeBooleanResult",
            () => Spec.From((int n) => n % 2 == 0).AsAllSatisfied().Create("all even").Evaluate(Models)),
        Of<string>("unnamed expression-tree higher order", "HigherOrderFromExpressionTreeExplanationPolicyResult",
            () => Spec.From((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create()
                .Evaluate(Models)),
        Of<string>("named expression-tree higher order", "HigherOrderFromExpressionTreeMetadataPolicyResult",
            () => Spec.From((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalse("some are odd")
                .Create("all even")
                .Evaluate(Models)),
        Of<string>("unnamed multi-assertion expression-tree higher order", "HigherOrderFromExpressionTreeMultiAssertionExplanationBooleanResult",
            () => Spec.From((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create()
                .Evaluate(Models)),
        Of<string>("named multi-assertion expression-tree higher order", "HigherOrderFromExpressionTreeMultiMetadataBooleanResult",
            () => Spec.From((int n) => n % 2 == 0)
                .AsAllSatisfied()
                .WhenTrue("all are even")
                .WhenFalseYield(_ => ["some are odd", "not all even"])
                .Create("all even")
                .Evaluate(Models)),
    ];

    public static TheoryData<string> CaseNames => NamesOf(Cases);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Repeated_reads_return_the_same_description_explanation_and_metadata_tier(string caseName) =>
        Named(Cases, caseName).AssertRepeatedReadsReturnTheSameInstancesAsync();
}
