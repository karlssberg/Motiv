using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    MetadataSet<TMetadata> metadataSet,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition,
    ReasonSource reasonSource)
    : BooleanResultBase<TMetadata>
{
    public override MetadataSet<TMetadata> Metadata => metadataSet;

    public override bool Satisfied => isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadataSet,
            causes,
            proposition,
            reasonSource);

    public override Explanation Explanation => 
        new (Description)
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
        };
}