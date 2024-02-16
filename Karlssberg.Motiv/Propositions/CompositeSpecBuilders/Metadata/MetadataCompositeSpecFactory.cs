using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Metadata;

public readonly struct MetadataCompositeSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new CompositeSpecFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            whenTrue,
            whenFalse,
            description);
}