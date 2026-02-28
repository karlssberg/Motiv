using Motiv.AndAlso;
using Motiv.Shared;

namespace Motiv.And;

internal sealed class AndBooleanResultDescription<TMetadata>(
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : BinaryBooleanResultDescription<TMetadata>(causalResults)
{
    internal override string Statement => Operator.And;

    protected override string Separator => " & ";

    protected override bool IsSameFamily(BooleanResultBase<TMetadata> result) =>
        result is AndBooleanResult<TMetadata> or AndAlsoBooleanResult<TMetadata>;
}
