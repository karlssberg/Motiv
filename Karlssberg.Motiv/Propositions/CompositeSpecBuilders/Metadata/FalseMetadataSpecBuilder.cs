namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Metadata;

public readonly struct FalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue)
{
    public MetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec, whenTrue, _ => whenFalse);

    public MetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(Func<TModel, TMetadata> whenFalse) =>
        new(spec, whenTrue, whenFalse);
}