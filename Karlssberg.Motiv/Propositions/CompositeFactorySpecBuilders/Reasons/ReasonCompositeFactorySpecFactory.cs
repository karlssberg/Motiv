namespace Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders.Reasons;

public readonly ref struct ReasonCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        CreateSpecInternal(proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
    
    private SpecBase<TModel, string> CreateSpecInternal(string name) =>
        new CompositeFactorySpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            trueBecause,
            falseBecause,
            name);
}