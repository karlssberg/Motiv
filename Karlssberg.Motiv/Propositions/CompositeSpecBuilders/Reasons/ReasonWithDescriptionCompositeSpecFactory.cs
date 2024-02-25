namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly ref struct ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string candidateName)
{
    public SpecBase<TModel, string> CreateSpec() =>
        CreateSpecInternal(candidateName);
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        CreateSpecInternal(proposition.ThrowIfNullOrWhitespace(nameof(proposition)));

    private SpecBase<TModel, string> CreateSpecInternal(string name) =>
        new CompositeFactorySpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            name);
}