using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.NestedSpecBuilders.Reasons;

public readonly struct NestedReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
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
            specPredicate,
            trueBecause,
            falseBecause,
            description);
}