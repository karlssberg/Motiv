using Motiv.Shared;

namespace Motiv.And;

/// <summary>
///     Represents the result of a boolean AND operation between two <see cref="BooleanResultBase{TMetadata}" />
///     objects.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class AndBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    public override bool Satisfied { get; } = left.Satisfied && right.Satisfied;

    public override ResultDescriptionBase Description =>
        field ??= new AndBooleanResultDescription<TMetadata>(CausalResults);

    public override string Operation => Operator.And;

    // Un-collapsing the 3,000-deep AND chain in DeepCompositionTests builds a justification whose memory grows with the
    // cube of its depth: it exhausts the runner before Stryker's timeout fires, which lost the operators shard
    // twice (#294). Collapsing is pinned by PropositionResultDescriptionTests.Should_collapse_AND_and_ANDALSO_operators_in_spec_result_description.
    // Stryker disable once Boolean : un-collapsing a deep chain exhausts the runner's memory
    public override bool IsCollapsable => true;
}
