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

    public override IAssertion Assertion =>
        new HigherOrderAssertion<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadataCollection,
            causes,
            proposition,
            reasonSource);

    public override Reason Reason => 
        new (Assertion)
        {
            Underlying = causes
                .Select(cause => cause.Reason)
        };

    public override CausalMetadata<TMetadata> CausalMetadata =>
        new(Metadata, Reason.Assertions)
        {
            Underlying = causes switch
            {
                IEnumerable<BooleanResultBase<TMetadata>> results => results.Select(result => result.CausalMetadata),
                _ => Enumerable.Empty<CausalMetadata<TMetadata>>()
            }
        };
}