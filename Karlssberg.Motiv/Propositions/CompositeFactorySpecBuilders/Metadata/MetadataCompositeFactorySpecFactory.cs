namespace Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders.Metadata;

public readonly struct MetadataCompositeFactorySpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse)
{
    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new CompositeSpecFactorySpec<TModel, TMetadata, TUnderlyingMetadata>(
            specPredicate,
            whenTrue,
            whenFalse,
            description.ThrowIfNullOrWhitespace(nameof(description)));

}