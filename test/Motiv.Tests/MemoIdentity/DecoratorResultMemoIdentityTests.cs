using static Motiv.Tests.MemoIdentity.MemoisedResultCase;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The decorator results — a spec or policy re-described by <c>Spec.Build(spec)</c> — keep their memoised
/// description, explanation and metadata tier across reads.
/// </summary>
public class DecoratorResultMemoIdentityTests
{
    private static readonly PolicyBase<bool, string> UnderlyingPolicy = Spec.Build((bool b) => b).Create("underlying");

    private static readonly SpecBase<bool, string> UnderlyingSpec = Spec.Build((bool b) => b).Create("underlying");

    private static readonly MemoisedResultCase[] Cases =
    [
        // Decorating a policy.
        Of<string>("minimal policy decorator", "MinimalPolicyDecoratorPolicyResult",
            () => Spec.Build(UnderlyingPolicy).Create("is accepted").Evaluate(true)),
        Of<string>("unnamed policy decorator", "PolicyDecoratorWithSingleTrueAssertionPolicyResult",
            () => Spec.Build(UnderlyingPolicy).WhenTrue("yes").WhenFalse("no").Create().Evaluate(true)),
        Of<string>("named policy decorator", "PolicyDecoratorPolicyResult",
            () => Spec.Build(UnderlyingPolicy).WhenTrue("yes").WhenFalse("no").Create("is accepted").Evaluate(true)),
        Of<string>("unnamed multi-assertion policy decorator", "PolicyDecoratorMultiAssertionExplanationBooleanResult",
            () => Spec.Build(UnderlyingPolicy)
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create()
                .Evaluate(false)),
        Of<string>("named multi-assertion policy decorator", "PolicyDecoratorMultiMetadataBooleanResult",
            () => Spec.Build(UnderlyingPolicy)
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create("is accepted")
                .Evaluate(false)),

        // Decorating a spec.
        Of<string>("minimal spec decorator", "MinimalSpecDecoratorBooleanResult",
            () => Spec.Build(UnderlyingSpec).Create("is accepted").Evaluate(true)),
        Of<string>("unnamed spec decorator", "SpecDecoratorWithSingleTrueAssertionPolicyResult",
            () => Spec.Build(UnderlyingSpec).WhenTrue("yes").WhenFalse("no").Create().Evaluate(true)),
        Of<string>("named spec decorator", "SpecDecoratorPolicyResult",
            () => Spec.Build(UnderlyingSpec).WhenTrue("yes").WhenFalse("no").Create("is accepted").Evaluate(true)),
        Of<string>("unnamed multi-assertion spec decorator", "SpecDecoratorMultiAssertionExplanationBooleanResult",
            () => Spec.Build(UnderlyingSpec)
                .WhenTrue("yes")
                .WhenFalseYield((_, _) => ["no", "not at all"])
                .Create()
                .Evaluate(false)),
        Of<string>("named multi-assertion spec decorator", "SpecDecoratorMultiMetadataBooleanResult",
            () => Spec.Build(UnderlyingSpec)
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
