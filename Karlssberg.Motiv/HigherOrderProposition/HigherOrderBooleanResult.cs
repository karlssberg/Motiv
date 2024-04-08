namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    IEnumerable<TMetadata> metadata,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes,
    IEnumerable<string> assertions,
    string reason)
    : BooleanResultBase<TMetadata>
{
    public override MetadataTree<TMetadata> MetadataTree => new(metadata);
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        underlyingResults.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => causes;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        causes.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
            reason,
            causes);

    public override Explanation Explanation => 
        new (assertions)
        {
            Underlying = causes
                .Select(cause => cause.Explanation)
                .ElseIfEmpty(underlyingResults.Select(result => result.Explanation))
        };
}