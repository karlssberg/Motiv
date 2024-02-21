namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Metadata;

public readonly ref struct MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse)
{
    public SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string description) =>
        new HigherOrderSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            description);
}