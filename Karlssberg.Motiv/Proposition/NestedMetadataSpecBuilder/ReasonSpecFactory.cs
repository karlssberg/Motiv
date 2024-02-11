using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Proposition.NestedMetadataSpecBuilder;

public readonly struct ReasonSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));
    
    private SpecBase<TModel, string> CreateSpecInternal(string description) =>
        new ChangeMetadataSpecFactorySpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            description);
}