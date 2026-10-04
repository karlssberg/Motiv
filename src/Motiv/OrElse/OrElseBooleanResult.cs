using Motiv.Shared;

namespace Motiv.OrElse;

internal sealed class OrElseBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    // right may be omitted only when left is satisfied; otherwise it is required.
    public override bool Satisfied { get; } =
        left.Satisfied
        || (right ?? throw new ArgumentNullException(
            nameof(right),
            "The right operand must be supplied when the left operand is unsatisfied.")).Satisfied;

    public override ResultDescriptionBase Description =>
        field ??= new OrElseBooleanResultDescription<TMetadata>(CausalResults);

    public override string Operation => Operator.OrElse;

    // Un-collapsing the 50,000-deep OR ELSE chain in DeepEvaluationTests builds a justification whose memory grows with the
    // cube of its depth: it exhausts the runner before Stryker's timeout fires, which lost the operators shard
    // twice (#294). Collapsing is pinned by OrElseSpecTests.Should_collapse_OR_ELSE_operators_in_a_non_policy_result_justification.
    // Stryker disable once Boolean : un-collapsing a deep chain exhausts the runner's memory
    public override bool IsCollapsable => true;
}
