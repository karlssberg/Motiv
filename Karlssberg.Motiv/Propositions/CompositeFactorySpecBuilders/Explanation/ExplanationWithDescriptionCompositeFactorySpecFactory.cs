namespace Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders.Explanation;

public readonly ref struct ExplanationWithDescriptionCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
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
            specPredicate,
            trueBecause,
            falseBecause,
            proposition);
}