using Karlssberg.Motiv.HigherOrder;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Reasons;

public readonly ref struct ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    string candidateProposition,
    ReasonSource reasonSource)
{
    public SpecBase<IEnumerable<TModel>, string> CreateSpec() =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateProposition,
            reasonSource);

    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string proposition) =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            reasonSource);
}