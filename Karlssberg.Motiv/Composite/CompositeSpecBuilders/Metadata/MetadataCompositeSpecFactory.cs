namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Metadata;

public readonly ref struct MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string proposition) =>
        new CompositeSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}