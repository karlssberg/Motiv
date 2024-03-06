using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Explanation;

public readonly ref struct FalseAssertionsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    string candidateName,
    ReasonSource reasonSource)
{
    public ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause.ToEnumerable(),
            candidateName,
            reasonSource);
    
    public ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            results => falseBecause(results).ToEnumerable(),
            candidateName,
            reasonSource);
    
    public ExplanationWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateName,
            ReasonSource.Proposition);
}