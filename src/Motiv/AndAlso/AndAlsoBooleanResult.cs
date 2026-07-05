using Motiv.Shared;

namespace Motiv.AndAlso;

internal sealed class AndAlsoBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    public override bool Satisfied { get; } = left.Satisfied && (right?.Satisfied ?? false);

    public override ResultDescriptionBase Description =>
        field ??= new AndAlsoBooleanResultDescription<TMetadata>(CausalResults);

    public override string Operation => Operator.AndAlso;

    public override bool IsCollapsable => true;
}
