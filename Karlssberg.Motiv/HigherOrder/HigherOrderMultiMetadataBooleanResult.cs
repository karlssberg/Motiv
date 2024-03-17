namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults,
    MetadataTree<TMetadata> metadataTree,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    public override MetadataTree<TMetadata> MetadataTree => metadataTree;
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        underlyingResults.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => causes;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        causes.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override bool Satisfied => isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
            proposition.ToReason(isSatisfied),
            causes);

    public override Explanation Explanation => 
        new (Description)
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
                .ElseIfEmpty(underlyingResults.Select(result => result.Explanation))
        };
}