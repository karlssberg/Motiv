namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Metadata;

public readonly ref struct FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue)
{
    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        TMetadata whenFalse) =>
        new(specPredicate,
            whenTrue,
            (_, _) => whenFalse.ToEnumerable());

    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, TMetadata> whenFalse) =>
        new(specPredicate,
            whenTrue,
            (model, _) => whenFalse(model).ToEnumerable());

    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(specPredicate,
            whenTrue,
            (model, result) => whenFalse(model, result).ToEnumerable());

    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(specPredicate,
            whenTrue,
            whenFalse);
}