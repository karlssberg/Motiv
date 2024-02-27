namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Reasons;

public readonly ref struct FalseReasonsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    string candidateName,
    ReasonSource reasonSource)
{
    public ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause.ToEnumerable(),
            candidateName,
            reasonSource);
    
    public ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            results => falseBecause(results).ToEnumerable(),
            candidateName,
            reasonSource);
    
    public ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateName,
            ReasonSource.Proposition);
}