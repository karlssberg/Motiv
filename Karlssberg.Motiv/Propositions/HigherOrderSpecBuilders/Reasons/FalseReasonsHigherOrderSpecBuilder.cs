namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Reasons;

public readonly ref struct FalseReasonsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause)
{
    public ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => [falseBecause]);
    
    public ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            reasons => [falseBecause(reasons)]);

    public ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause);
}