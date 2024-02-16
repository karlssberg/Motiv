namespace Karlssberg.Motiv.Proposition.SpecBuilders;

public readonly struct FalseReasonsWithDescriptionSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    string candidateDescription)
{
    public ReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            trueBecause,
            _ => falseBecause,
            candidateDescription);

    public ReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause,
            candidateDescription);
}

public readonly struct FalseReasonsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    string candidateDescription)
{
    public ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            _ => [falseBecause],
            candidateDescription);
    
    public ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            results => [falseBecause(results)],
            candidateDescription);
    
    public ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateDescription);
}