using Karlssberg.Motiv.CompositeFactory;

namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Metadata;

public readonly ref struct MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string proposition) =>
        new CompositeFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}