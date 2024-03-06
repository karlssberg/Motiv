using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Metadata;

public readonly ref struct FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue)
{
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(TMetadata whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            _ => whenFalse.ToEnumerable());
    
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            results => whenFalse(results).ToEnumerable());
    
    public MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            whenFalse);
}