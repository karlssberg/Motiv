using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Metadata;

public readonly ref struct MetadataHigherOrderSpecFactory<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse)
{
    public SpecBase<IEnumerable<TModel>, TMetadata> CreateSpec(string proposition) =>
        new HigherOrderSpec<TModel, TMetadata, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            whenTrue,
            whenFalse,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            ReasonSource.Proposition);
}