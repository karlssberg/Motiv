using Motiv.And;
using Motiv.Shared;

namespace Motiv.AndAlso;

internal sealed class AndAlsoBooleanResultDescription<TMetadata>(
    IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    : BinaryBooleanResultDescription<TMetadata>(causalResults)
{
    internal override string Statement => Operator.AndAlso;

    protected override string Separator => " && ";

    protected override bool IsSameFamily(BooleanResultBase<TMetadata> result) =>
        result is AndBooleanResult<TMetadata> or AndAlsoBooleanResult<TMetadata>;
}
