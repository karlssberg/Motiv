using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults,
    MetadataTree<TMetadata> metadataTree,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition,
    AssertionSource assertionSource)
    : BooleanResultBase<TMetadata>
{
    public override MetadataTree<TMetadata> MetadataTree => metadataTree;
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        underlyingResults switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    public override IEnumerable<BooleanResultBase> Causes => causes;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        causes switch
        {
            IEnumerable<BooleanResultBase<TMetadata>> results => results,
            _ => Enumerable.Empty<BooleanResultBase<TMetadata>>()
        };
    
    public override bool Satisfied => isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadataTree,
            causes,
            proposition,
            assertionSource);

    public override Explanation Explanation => 
        new (Description)
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
        };
}