namespace Karlssberg.Motiv.Proposition.SpecBuilders;

public readonly struct FalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue)
{
    public MetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec, whenTrue, _ => whenFalse);

    public MetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse) =>
        new(spec, whenTrue, whenFalse);
}

public readonly struct FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue)
{
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec, higherOrderPredicate, whenTrue, _ => [whenFalse]);
    
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(spec, higherOrderPredicate, whenTrue, results => [whenFalse(results)]);
    
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(spec, higherOrderPredicate, whenTrue, whenFalse);
}