using Motiv.OrElse;
using Motiv.Shared;

namespace Motiv.Or;

internal sealed class OrBooleanResultDescription<TMetadata>(
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : BinaryBooleanResultDescription<TMetadata>(causalResults)
{
    internal override string Statement => Operator.Or;

    protected override string Separator => " | ";

    protected override bool IsSameFamily(BooleanResultBase<TMetadata> result) =>
        result is OrBooleanResult<TMetadata> or OrElseBooleanResult<TMetadata>;
}
