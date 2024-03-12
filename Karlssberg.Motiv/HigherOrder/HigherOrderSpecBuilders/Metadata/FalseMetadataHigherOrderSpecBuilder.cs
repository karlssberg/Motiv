namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Metadata;

public readonly ref struct FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse.ToEnumerable(),
            causeSelector);
    
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            results => whenFalse(results).ToEnumerable(),
            causeSelector);
    
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            causeSelector);
}