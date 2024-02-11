using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.NestedMetadataSpecBuilder;

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