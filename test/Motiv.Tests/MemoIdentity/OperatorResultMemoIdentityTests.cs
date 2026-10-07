using static Motiv.Tests.MemoIdentity.MemoIdentityPropositions;
using static Motiv.Tests.MemoIdentity.MemoisedResultCase;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The operator results — reached from sync, async and expression-tree specs and policies, and from the
/// result-level operators — keep their memoised description, explanation and metadata tier across reads.
/// <para>
/// A performance guard, like every test in this folder: a rebuild is an equal value, so these pin the cost
/// of a re-read, not its output (see <see cref="MemoisedResultCase" />).
/// </para>
/// </summary>
public class OperatorResultMemoIdentityTests
{
    private static PolicyBase<bool, int> MetadataPolicy() =>
        Spec.Build((bool b) => b).WhenTrue(1).WhenFalse(0).Create("is true");

    // AndAlso/OrElse policy results re-derive their causes and operands on every read.
    private static readonly string[] ShortCircuitPolicyRebuilds = ["Causes", "Underlying"];

    private static readonly MemoisedResultCase[] Cases =
    [
        // Sync specs and policies.
        Of<string>("and spec", "AndBooleanResult",
            () => Proposition("left").And(Proposition("right")).Evaluate(true)),
        Of<string>("or spec", "OrBooleanResult",
            () => Proposition("left").Or(Proposition("right")).Evaluate(false)),
        Of<string>("xor spec", "XOrBooleanResult",
            () => Proposition("left").XOr(Proposition("right")).Evaluate(true)),
        Of<string>("and-also spec", "AndAlsoBooleanResult",
            () => Proposition("left").AndAlso(Proposition("right")).Evaluate(true)),
        Of<string>("or-else spec", "OrElseBooleanResult",
            () => Proposition("left").OrElse(Proposition("right")).Evaluate(false)),
        Of<string>("not spec", "NotBooleanOperationResult",
            () => Proposition("operand").Not().Evaluate(true)),
        Of<string>("not policy", "NotPolicyResult",
            () => Policy("operand").Not().Evaluate(true)),
        Of<string>("and-also policy", "AndAlsoPolicyResult",
            () => Policy("left").AndAlso(Policy("right")).Evaluate(true),
            ShortCircuitPolicyRebuilds),
        Of<string>("or-else policy", "OrElsePolicyResult",
            () => Policy("left").OrElse(Policy("right")).Evaluate(false),
            ShortCircuitPolicyRebuilds),

        // Async specs and policies.
        OfAsync<string>("async and spec", "AndBooleanResult",
            async () => await AsyncProposition("left").And(AsyncProposition("right")).EvaluateAsync(true)),
        OfAsync<string>("async or spec", "OrBooleanResult",
            async () => await AsyncProposition("left").Or(AsyncProposition("right")).EvaluateAsync(false)),
        OfAsync<string>("async xor spec", "XOrBooleanResult",
            async () => await AsyncProposition("left").XOr(AsyncProposition("right")).EvaluateAsync(true)),
        OfAsync<string>("async and-also spec", "AndAlsoBooleanResult",
            async () => await AsyncProposition("left").AndAlso(AsyncProposition("right")).EvaluateAsync(true)),
        OfAsync<string>("async or-else spec", "OrElseBooleanResult",
            async () => await AsyncProposition("left").OrElse(AsyncProposition("right")).EvaluateAsync(false)),
        OfAsync<string>("async not spec", "NotBooleanOperationResult",
            async () => await AsyncProposition("operand").Not().EvaluateAsync(true)),
        OfAsync<string>("async not policy", "NotPolicyResult",
            async () => await AsyncPolicy("operand").Not().EvaluateAsync(true)),
        OfAsync<string>("async and-also policy", "AndAlsoPolicyResult",
            async () => await AsyncPolicy("left").AndAlso(AsyncPolicy("right")).EvaluateAsync(true),
            ShortCircuitPolicyRebuilds),
        OfAsync<string>("async or-else policy", "OrElsePolicyResult",
            async () => await AsyncPolicy("left").OrElse(AsyncPolicy("right")).EvaluateAsync(false),
            ShortCircuitPolicyRebuilds),

        // Expression-tree specs and policies.
        Of<string>("expression and spec", "AndBooleanResult",
            () => ExpressionProposition("left").And(ExpressionProposition("right")).Evaluate(1)),
        Of<string>("expression or spec", "OrBooleanResult",
            () => ExpressionProposition("left").Or(ExpressionProposition("right")).Evaluate(-1)),
        Of<string>("expression xor spec", "XOrBooleanResult",
            () => ExpressionProposition("left").XOr(ExpressionProposition("right")).Evaluate(1)),
        Of<string>("expression and-also spec", "AndAlsoBooleanResult",
            () => ExpressionProposition("left").AndAlso(ExpressionProposition("right")).Evaluate(1)),
        Of<string>("expression or-else spec", "OrElseBooleanResult",
            () => ExpressionProposition("left").OrElse(ExpressionProposition("right")).Evaluate(-1)),
        Of<string>("expression not spec", "NotBooleanOperationResult",
            () => ExpressionProposition("operand").Not().Evaluate(1)),
        Of<string>("expression not policy", "NotPolicyResult",
            () => ExpressionPolicy("positive").Not().Evaluate(1)),
        Of<string>("expression and-also policy", "AndAlsoPolicyResult",
            () => ExpressionPolicy("positive").AndAlso(ExpressionPolicy("positive")).Evaluate(1),
            ShortCircuitPolicyRebuilds),
        Of<string>("expression or-else policy", "OrElsePolicyResult",
            () => ExpressionPolicy("positive").OrElse(ExpressionPolicy("positive")).Evaluate(-1),
            ShortCircuitPolicyRebuilds),

        // Result-level operators.
        Of<string>("and result", "AndBooleanResult",
            () => Proposition("left").Evaluate(true) & Proposition("right").Evaluate(false)),
        Of<string>("or result", "OrBooleanResult",
            () => Proposition("left").Evaluate(true) | Proposition("right").Evaluate(false)),
        Of<string>("xor result", "XOrBooleanResult",
            () => Proposition("left").Evaluate(true) ^ Proposition("right").Evaluate(false)),
        Of<string>("and-also result", "AndAlsoBooleanResult",
            () => Proposition("left").Evaluate(true).AndAlso(Proposition("right").Evaluate(true))),
        Of<string>("or-else result", "OrElseBooleanResult",
            () => Proposition("left").Evaluate(false).OrElse(Proposition("right").Evaluate(false))),
        Of<string>("not result", "NotBooleanOperationResult",
            () => !Proposition("operand").Evaluate(true)),
        Of<string>("not policy result", "NotPolicyResult",
            () => !Policy("operand").Evaluate(true)),
        Of<string>("and-also policy result", "AndAlsoPolicyResult",
            () => Policy("left").Evaluate(true).AndAlso(Policy("right").Evaluate(false)),
            ShortCircuitPolicyRebuilds),
        Of<string>("or-else policy result", "OrElsePolicyResult",
            () => Policy("left").Evaluate(false).OrElse(Policy("right").Evaluate(true)),
            ShortCircuitPolicyRebuilds),

        // Mixing metadata types wraps each operand in an explanation result.
        Of<string>("explanation operand of a mixed-metadata and", "ExplanationBooleanResult",
            () => MetadataPolicy().Evaluate(true).And(Policy("right").Evaluate(true)).UnderlyingWithValues.First()),
    ];

    public static TheoryData<string> CaseNames => NamesOf(Cases);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public Task Repeated_reads_return_the_same_description_explanation_and_metadata_tier(string caseName) =>
        Named(Cases, caseName).AssertRepeatedReadsReturnTheSameInstancesAsync();
}
