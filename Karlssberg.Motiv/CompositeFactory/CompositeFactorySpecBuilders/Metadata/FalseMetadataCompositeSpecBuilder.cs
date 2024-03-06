namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Metadata;

public readonly ref struct FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, TMetadata> whenTrue)
{
    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(specPredicate,
            whenTrue,
            _ => whenFalse);

    public MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse) =>
        new(specPredicate,
            whenTrue,
            whenFalse);
}