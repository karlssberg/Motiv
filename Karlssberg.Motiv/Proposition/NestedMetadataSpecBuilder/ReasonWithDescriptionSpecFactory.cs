using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.NestedMetadataSpecBuilder;

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