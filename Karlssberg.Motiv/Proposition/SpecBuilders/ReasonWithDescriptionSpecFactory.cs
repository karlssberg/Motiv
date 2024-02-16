using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.SpecBuilders;

public readonly struct ReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string candidateDescription)
{
    public SpecBase<TModel, string> CreateSpec() =>
        CreateSpecInternal(candidateDescription);
    
    public SpecBase<TModel, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));

    private SpecBase<TModel, string> CreateSpecInternal(string description) =>
        new ChangeMetadataSpecFactorySpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            description);
}

public readonly struct ReasonWithDescriptionHigherOrderSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    string candidateDescription)
{
    public SpecBase<IEnumerable<TModel>, string> CreateSpec() =>
        CreateSpecInternal(candidateDescription);
    
    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));

    private SpecBase<IEnumerable<TModel>, string> CreateSpecInternal(string description) =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            description);
}