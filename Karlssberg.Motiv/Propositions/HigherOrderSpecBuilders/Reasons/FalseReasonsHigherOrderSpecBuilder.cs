namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Reasons;

public readonly ref struct FalseReasonsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    ReasonSource reasonSource)
{
    public ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause.ToEnumerable(),
            reasonSource);

    public ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            reasons => falseBecause(reasons).ToEnumerable(),
            reasonSource);

    public ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            ReasonSource.Proposition);
}