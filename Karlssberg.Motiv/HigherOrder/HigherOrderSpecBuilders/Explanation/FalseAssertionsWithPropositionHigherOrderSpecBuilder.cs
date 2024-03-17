namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;

public readonly ref struct FalseAssertionsWithPropositionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    string candidateName,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public ExplanationWithPropositionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause,
            candidateName,
            causeSelector);
    
    public ExplanationWithPropositionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateName,
            causeSelector);
    
    public ExplanationMultiAssertionWithPropositionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new (spec,
            higherOrderPredicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause,
            candidateName,
            causeSelector);
}