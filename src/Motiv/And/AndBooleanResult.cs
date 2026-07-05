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

    public override bool IsCollapsable => true;
}
