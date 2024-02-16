namespace Karlssberg.Motiv.Proposition.HigherOrderSpecBuilders.Reasons;

public readonly struct ReasonHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause)
{
    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string description) =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            description.ThrowIfNullOrWhitespace(nameof(description)));
}