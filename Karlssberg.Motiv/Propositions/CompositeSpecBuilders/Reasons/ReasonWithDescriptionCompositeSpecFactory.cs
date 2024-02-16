using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly struct ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata>(
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
        new CompositeSpecFactorySpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            description);
}