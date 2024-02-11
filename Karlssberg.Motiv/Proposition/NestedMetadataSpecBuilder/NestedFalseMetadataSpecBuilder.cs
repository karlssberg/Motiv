namespace Karlssberg.Motiv.Proposition.NestedMetadataSpecBuilder;

public readonly struct NestedFalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, TMetadata> whenTrue)
{
    public NestedMetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata> YieldWhenFalse(TMetadata whenFalse) =>
        new(specPredicate, whenTrue, _ => whenFalse);

    public NestedMetadataSpecFactory<TModel, TMetadata, TUnderlyingMetadata> YieldWhenFalse(Func<TModel, TMetadata> whenFalse) =>
        new(specPredicate, whenTrue, whenFalse);
}