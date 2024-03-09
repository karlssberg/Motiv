namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;

public readonly ref struct ExplanationCompositeFactorySpecFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause)
{
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new CompositeFactorySpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}