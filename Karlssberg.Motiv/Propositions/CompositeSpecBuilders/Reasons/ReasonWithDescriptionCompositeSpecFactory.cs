namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly ref struct ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    string candidateProposition)
{
    public SpecBase<TModel, string> CreateSpec() =>
        CreateSpecInternal(candidateProposition);
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        CreateSpecInternal(proposition.ThrowIfNullOrWhitespace(nameof(proposition)));

    private SpecBase<TModel, string> CreateSpecInternal(string proposition) =>
        new CompositeFactorySpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            proposition);
}