namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    Func<IEnumerable<TMetadata>> metadataFn,
    Func<IEnumerable<string>> assertionsFn,
    Func<string> reasonFn,
    IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causesFn)
    : BooleanResultBase<TMetadata>
{
    private readonly Lazy<MetadataTree<TMetadata>> _metadataTree = new (() =>
        new MetadataTree<TMetadata>(
            metadataFn(),
            causesFn().SelectMany(cause => cause
                    .ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>())));
    
    private readonly Lazy<Explanation> _explanation = 
        new (() => new Explanation(assertionsFn(), causesFn()));

    public override MetadataTree<TMetadata> MetadataTree => _metadataTree.Value;
    
    public override Explanation Explanation => _explanation.Value;
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        underlyingResults.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override IEnumerable<BooleanResultBase> Causes => causesFn();
    
    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        causesFn().ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();
    
    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description =>
        new HigherOrderResultDescription<TModel, TUnderlyingMetadata>(
            reasonFn(),
            causesFn());
}