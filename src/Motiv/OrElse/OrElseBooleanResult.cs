using Motiv.Shared;

namespace Motiv.OrElse;

internal sealed class OrElseBooleanResult<TMetadata>(
    BooleanResultBase<TMetadata> left,
    BooleanResultBase<TMetadata>? right = null)
    : BinaryBooleanResult<TMetadata>(left, right)
{
    public override bool Satisfied { get; } = left.Satisfied || (right?.Satisfied ?? false);

    public override ResultDescriptionBase Description =>
        new OrElseBooleanResultDescription<TMetadata>(GetCausalResults());

    public override string Operation => Operator.OrElse;

    public override bool IsCollapsable => true;
}
