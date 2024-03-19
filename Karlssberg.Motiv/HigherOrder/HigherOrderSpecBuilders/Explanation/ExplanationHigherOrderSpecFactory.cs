namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;

public readonly ref struct ExplanationHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public SpecBase<IEnumerable<TModel>, string> Create(string proposition) =>
        new HigherOrderMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            causeSelector);
}