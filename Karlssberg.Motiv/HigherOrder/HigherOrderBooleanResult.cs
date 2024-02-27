using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition,
    ReasonSource reasonSource)
    : BooleanResultBase<TMetadata>
{
    public override MetadataSet<TMetadata> Metadata => new(metadataCollection);


    public override bool Satisfied => isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadataCollection,
            causes,
            proposition,
            reasonSource);

    public override Explanation Explanation => 
        new (Description)
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
        };

    public override CausalMetadata<TMetadata> CausalMetadata =>
        new(Metadata, Explanation.Assertions)
        {
            Underlying = causes switch
            {
                IEnumerable<BooleanResultBase<TMetadata>> results => results.Select(result => result.CausalMetadata),
                _ => Enumerable.Empty<CausalMetadata<TMetadata>>()
            }
        };
}