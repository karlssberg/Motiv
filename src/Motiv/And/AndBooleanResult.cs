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

    private ResultDescriptionBase? _description;
    public override ResultDescriptionBase Description =>
        _description ??= new AndBooleanResultDescription<TMetadata>(GetCausalResults());

    public override string Operation => Operator.And;

    public override bool IsCollapsable => true;
}
