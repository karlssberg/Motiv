namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;

public readonly ref struct ExplanationWithDescriptionCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
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