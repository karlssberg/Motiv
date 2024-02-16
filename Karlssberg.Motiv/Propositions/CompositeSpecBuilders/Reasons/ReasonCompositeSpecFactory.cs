using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly struct ReasonCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));
    
    private SpecBase<TModel, string> CreateSpecInternal(string description) =>
        new CompositeSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            description);
}