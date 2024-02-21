namespace Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders.Reasons;

public readonly ref struct FalseReasonsCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause)
{
    public ReasonCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(specPredicate,
            trueBecause,
            _ => falseBecause);

    public ReasonCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            falseBecause);
}