namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    Func<IEnumerable<TMetadata>> metadata,
    Func<IEnumerable<string>> assertions,
    Func<string> reason,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causes)
    : BooleanResultBase<TMetadata>
{
    private readonly IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> causes;

    private readonly Lazy<MetadataTree<TMetadata>> _metadataTree = new (() =>
        new MetadataTree<TMetadata>(
            metadata(),
            causes().SelectMany(cause => cause
                    .ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>())));
    
    private readonly Lazy<Explanation> _explanation = new (() =>
        new Explanation(assertions())
        {
            Underlying = causes()
                .Select(cause => cause.Explanation)
                .ElseIfEmpty(underlyingResults.Select(result => result.Explanation))
        });

    public override MetadataTree<TMetadata> MetadataTree => _metadataTree.Value;
    
    public override Explanation Explanation => _explanation.Value;

    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;
    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        underlyingResults.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => causes;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        causes.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
            reason(),
            causes());
}