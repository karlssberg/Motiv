using Motiv.Shared;

namespace Motiv.AndAlso;

internal sealed class AndAlsoBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    // right may be omitted only when left is unsatisfied; otherwise it is required.
    public override bool Satisfied { get; } =
        left.Satisfied
        && (right ?? throw new ArgumentNullException(
            nameof(right),
            "The right operand must be supplied when the left operand is satisfied.")).Satisfied;

    public override ResultDescriptionBase Description =>
        field ??= new AndAlsoBooleanResultDescription<TMetadata>(CausalResults);

    public override string Operation => Operator.AndAlso;

    public override bool IsCollapsable => true;
}
