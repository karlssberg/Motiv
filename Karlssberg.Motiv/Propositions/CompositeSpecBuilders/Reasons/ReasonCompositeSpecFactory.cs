namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly ref struct ReasonCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        CreateSpecInternal(proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
    
    private SpecBase<TModel, string> CreateSpecInternal(string name) =>
        new CompositeSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            name);
}