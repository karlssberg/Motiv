using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.SpecBuilders;

public readonly struct MetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new ChangeMetadataSpecFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            whenTrue,
            whenFalse,
            description);
}

public readonly struct MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
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