namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;

public readonly ref struct ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause)
{
    public SpecBase<TModel, string> Create(string proposition) =>
        new CompositeFactoryExplanationSpec<TModel, TUnderlyingMetadata>(
            specPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}