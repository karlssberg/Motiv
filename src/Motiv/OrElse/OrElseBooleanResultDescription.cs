using Motiv.Or;
using Motiv.Shared;

namespace Motiv.OrElse;

internal sealed class OrElseBooleanResultDescription<TMetadata>(
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : BinaryBooleanResultDescription<TMetadata>(causalResults)
{
    internal override string Statement => Operator.OrElse;

    protected override string Separator => " || ";

    protected override bool IsSameFamily(BooleanResultBase<TMetadata> result) =>
        result is OrBooleanResult<TMetadata> or OrElsePolicyResult<TMetadata> or OrElseBooleanResult<TMetadata>;
}
