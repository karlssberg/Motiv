namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;

public readonly ref struct ExplanationWithPropositionHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause,
    string candidateProposition,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public SpecBase<IEnumerable<TModel>, string> Create() =>
        new HigherOrderExplanationSpec<TModel,TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateProposition,
            causeSelector);

    public SpecBase<IEnumerable<TModel>, string> Create(string proposition) =>
        new HigherOrderMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            causeSelector);
}