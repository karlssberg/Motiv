using static Motiv.Tests.MemoIdentity.MemoisedResultCase;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The result-predicate results — a proposition built over a <c>model =&gt; spec.Evaluate(model)</c>
/// delegate — keep their memoised description, explanation and metadata tier across reads.
/// <para>
/// A performance guard, like every test in this folder: a rebuild is an equal value, so these pin the cost
/// of a re-read, not its output (see <see cref="MemoisedResultCase" />).
/// </para>
/// </summary>
public class ResultPredicateResultMemoIdentityTests
{
    private static readonly PolicyBase<bool, string> UnderlyingPolicy = Spec.Build((bool b) => b).Create("underlying");

    private static readonly SpecBase<bool, string> UnderlyingSpec = Spec.Build((bool b) => b).Create("underlying");

    private static readonly MemoisedResultCase[] Cases =
    [
        // Over a boolean result.
        Of<string>("minimal boolean-result predicate", "MinimalBooleanResultPredicateBooleanResult",
            () => Spec.Build((bool b) => UnderlyingSpec.Evaluate(b)).Create("is accepted").Evaluate(true)),
        Of<string>("unnamed boolean-result predicate", "BooleanResultPredicateWithSingleAssertionPolicyResult",
            () => Spec.Build((bool b) => UnderlyingSpec.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalse("no")
                .Create()
                .Evaluate(true)),
        Of<string>("named boolean-result predicate", "BooleanResultPredicatePolicyResult",
            () => Spec.Build((bool b) => UnderlyingSpec.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalse("no")
                .Create("is accepted")
                .Evaluate(true)),
        Of<string>("unnamed multi-assertion boolean-result predicate", "BooleanResultPredicateMultiAssertionExplanationBooleanResult",
            () => Spec.Build((bool b) => UnderlyingSpec.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create()
                .Evaluate(false)),
        Of<string>("named multi-assertion boolean-result predicate", "BooleanResultPredicateMultiValueBooleanResult",
            () => Spec.Build((bool b) => UnderlyingSpec.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create("is accepted")
                .Evaluate(false)),

        // Over a policy result.
        Of<string>("minimal policy-result predicate", "MinimalPolicyResultPredicatePolicyResult",
            () => Spec.Build((bool b) => UnderlyingPolicy.Evaluate(b)).Create("is accepted").Evaluate(true)),
        Of<string>("unnamed policy-result predicate", "PolicyResultPredicateWithSingleAssertionPolicyResult",
            () => Spec.Build((bool b) => UnderlyingPolicy.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalse("no")
                .Create()
                .Evaluate(true)),
        Of<string>("named policy-result predicate", "PolicyResultPredicatePolicyResult",
            () => Spec.Build((bool b) => UnderlyingPolicy.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalse("no")
                .Create("is accepted")
                .Evaluate(true)),
        Of<string>("unnamed multi-assertion policy-result predicate", "PolicyResultPredicateMultiAssertionExplanationBooleanResult",
            () => Spec.Build((bool b) => UnderlyingPolicy.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create()
                .Evaluate(false)),
        Of<string>("named multi-assertion policy-result predicate", "PolicyResultPredicateMultiValueBooleanResult",
            () => Spec.Build((bool b) => UnderlyingPolicy.Evaluate(b))
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create("is accepted")
                .Evaluate(false)),
    ];

    public static TheoryData<string> CaseNames => NamesOf(Cases);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Repeated_reads_return_the_same_description_explanation_and_metadata_tier(string caseName) =>
        Named(Cases, caseName).AssertRepeatedReadsReturnTheSameInstancesAsync();
}
