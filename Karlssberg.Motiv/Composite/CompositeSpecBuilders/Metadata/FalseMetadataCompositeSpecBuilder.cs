namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Metadata;

public readonly ref struct FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue)
{
    public MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        TMetadata whenFalse) =>
        new(spec,
            whenTrue,
            (_, _) => whenFalse.ToEnumerable());
    
    public MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, TMetadata> whenFalse) =>
        new(spec,
            whenTrue,
            (model, _) => whenFalse(model).ToEnumerable());
    
    public MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(spec,
            whenTrue,
            (model, result) => whenFalse(model, result).ToEnumerable());

    public MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(spec,
            whenTrue,
            whenFalse);
}