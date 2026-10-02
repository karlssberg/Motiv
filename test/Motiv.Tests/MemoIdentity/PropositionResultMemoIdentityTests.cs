using static Motiv.Tests.MemoIdentity.MemoisedResultCase;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The boolean-predicate leaf results — minimal, explanation and metadata propositions, and the adapter
/// that presents a metadata result as an explanation — keep their memoised description, explanation and
/// metadata tier across reads.
/// </summary>
public class PropositionResultMemoIdentityTests
{
    private static readonly MemoisedResultCase[] Cases =
    [
        Of<string>("minimal proposition", "MinimalPropositionPolicyResult",
            () => Spec.Build((bool b) => b).Create("is true").Evaluate(true)),
        Of<string>("unnamed explanation proposition", "ExplanationPropositionPolicyResult",
            () => Spec.Build((bool b) => b).WhenTrue("yes").WhenFalse("no").Create().Evaluate(true)),
        Of<string>("named explanation proposition", "MetadataPropositionPolicyResult",
            () => Spec.Build((bool b) => b).WhenTrue("yes").WhenFalse("no").Create("is true").Evaluate(true)),
        Of<int>("metadata proposition", "MetadataPropositionPolicyResult",
            () => Spec.Build((bool b) => b).WhenTrue(1).WhenFalse(0).Create("is true").Evaluate(false)),
        Of<string>("unnamed multi-assertion proposition", "MultiAssertionExplanationPropositionBooleanResult",
            () => Spec.Build((bool b) => b)
                .WhenTrue("yes")
                .WhenFalseYield(_ => ["no", "definitely not"])
                .Create()
                .Evaluate(false)),
        Of<string>("named multi-assertion proposition", "MultiValuePropositionBooleanResult",
            () => Spec.Build((bool b) => b)
                .WhenTrue("yes")
                .WhenFalseYield(_ => ["no", "definitely not"])
                .Create("is true")
                .Evaluate(false)),
        Of<string>("metadata proposition read as an explanation", "MetadataToExplanationAdapterBooleanResult",
            () => Spec.Build((bool b) => b).WhenTrue(1).WhenFalse(0).Create("is true").ToExplanationSpec().Evaluate(true)),
    ];

    public static TheoryData<string> CaseNames => NamesOf(Cases);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Repeated_reads_return_the_same_description_explanation_and_metadata_tier(string caseName) =>
        Named(Cases, caseName).AssertRepeatedReadsReturnTheSameInstancesAsync();
}
