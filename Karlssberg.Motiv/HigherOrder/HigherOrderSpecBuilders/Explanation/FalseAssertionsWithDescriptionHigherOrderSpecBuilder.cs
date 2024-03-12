namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;

public readonly ref struct FalseAssertionsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    string candidateName,
    AssertionSource assertionSource,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
{
    public ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause.ToEnumerable(),
            candidateName,
            assertionSource,
            causeSelector);
    
    public ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            results => falseBecause(results).ToEnumerable(),
            candidateName,
            assertionSource,
            causeSelector);
    
    public ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateName,
            AssertionSource.Proposition,
            causeSelector);
}