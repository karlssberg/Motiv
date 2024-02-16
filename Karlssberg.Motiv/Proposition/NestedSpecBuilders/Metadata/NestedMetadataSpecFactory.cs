using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.NestedSpecBuilders.Metadata;

public readonly struct NestedMetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new ChangeMetadataSpecFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
            specPredicate,
            whenTrue,
            whenFalse,
            description.ThrowIfNullOrWhitespace(nameof(description)));

}