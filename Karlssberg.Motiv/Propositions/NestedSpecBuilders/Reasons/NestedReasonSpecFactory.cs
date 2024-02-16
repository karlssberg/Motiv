using Karlssberg.Motiv.ChangeMetadataType;

namespace Karlssberg.Motiv.Propositions.NestedSpecBuilders.Reasons;

public readonly struct NestedReasonSpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));
    
    private SpecBase<TModel, string> CreateSpecInternal(string description) =>
        new ChangeMetadataSpecFactorySpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            trueBecause,
            falseBecause,
            description);
}