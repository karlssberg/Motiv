using static Motiv.Tests.MemoIdentity.MemoisedResultCase;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The expression-tree results — <c>Spec.From(expression)</c> without a higher-order operation — keep
/// their memoised description, explanation and metadata tier across reads.
/// </summary>
public class ExpressionTreeResultMemoIdentityTests
{
    private static readonly MemoisedResultCase[] Cases =
    [
        Of<string>("minimal expression tree", "MinimalExpressionTreePropositionBooleanResult",
            () => Spec.From((int n) => n > 0).Create("is positive").Evaluate(1)),
        Of<string>("unnamed delegate-explanation expression tree", "ExpressionTreeExplanationPropositionPolicyResult",
            () => Spec.From((int n) => n > 0)
                .WhenTrue(_ => "positive")
                .WhenFalse(_ => "not positive")
                .Create()
                .Evaluate(1)),
        Of<string>("unnamed explanation expression tree", "ExpressionTreeWithSingleTrueAssertionPropositionPolicyResult",
            () => Spec.From((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create().Evaluate(1)),
        Of<string>("named explanation expression tree", "ExpressionTreeMetadataPropositionPolicyResult",
            () => Spec.From((int n) => n > 0)
                .WhenTrue("positive")
                .WhenFalse("not positive")
                .Create("is positive")
                .Evaluate(1)),
        Of<int>("metadata expression tree", "ExpressionTreeMetadataPropositionPolicyResult",
            () => Spec.From((int n) => n > 0).WhenTrue(1).WhenFalse(0).Create("is positive").Evaluate(-1)),
        Of<string>("unnamed multi-assertion expression tree", "ExpressionTreeMultiAssertionExplanationPropositionBooleanResult",
            () => Spec.From((int n) => n > 0)
                .WhenTrue("positive")
                .WhenFalseYield((_, _) => ["not positive", "zero or less"])
                .Create()
                .Evaluate(-1)),
        Of<string>("named multi-assertion expression tree", "ExpressionTreeMultiMetadataPropositionBooleanResult",
            () => Spec.From((int n) => n > 0)
                .WhenTrue("positive")
                .WhenFalseYield((_, _) => ["not positive", "zero or less"])
                .Create("is positive")
                .Evaluate(-1)),
    ];

    public static TheoryData<string> CaseNames => NamesOf(Cases);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Repeated_reads_return_the_same_description_explanation_and_metadata_tier(string caseName) =>
        Named(Cases, caseName).AssertRepeatedReadsReturnTheSameInstancesAsync();
}
