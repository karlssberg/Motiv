using Motiv.Shared;

namespace Motiv.Or;

/// <summary>Represents a boolean result that is the logical OR of two operand results.</summary>
/// <typeparam name="TMetadata">The type of metadata associated with the boolean result.</typeparam>
internal sealed class OrBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata> right)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    public override bool Satisfied { get; } = left.Satisfied || right.Satisfied;

    public override ResultDescriptionBase Description =>
        new OrBooleanResultDescription<TMetadata>(GetCausalResults());

    public override string Operation => Operator.Or;

    public override bool IsCollapsable => true;
}
