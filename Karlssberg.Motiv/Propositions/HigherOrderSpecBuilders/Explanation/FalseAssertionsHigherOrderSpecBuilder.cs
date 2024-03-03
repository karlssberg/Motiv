using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Explanation;

public readonly ref struct FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    ReasonSource reasonSource)
{
    public ExplanationHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause.ToEnumerable(),
            reasonSource);

    public ExplanationHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            reasons => falseBecause(reasons).ToEnumerable(),
            reasonSource);

    public ExplanationHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            ReasonSource.Proposition);
}