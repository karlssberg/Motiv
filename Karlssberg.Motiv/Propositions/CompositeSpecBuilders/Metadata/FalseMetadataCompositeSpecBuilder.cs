namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Metadata;

public readonly ref struct FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue)
{
    public MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec,
            whenTrue,
            _ => whenFalse);

    public MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse) =>
        new(spec,
            whenTrue,
            whenFalse);
}