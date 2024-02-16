namespace Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders.Reasons;

public readonly struct ReasonCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string description) =>
        CreateSpecInternal(description.ThrowIfNullOrWhitespace(nameof(description)));
    
    private SpecBase<TModel, string> CreateSpecInternal(string description) =>
        new CompositeSpecFactorySpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            trueBecause,
            falseBecause,
            description);
}