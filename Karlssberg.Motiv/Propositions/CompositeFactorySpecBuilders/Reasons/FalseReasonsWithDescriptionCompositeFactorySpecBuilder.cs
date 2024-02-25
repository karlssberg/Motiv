namespace Karlssberg.Motiv.Propositions.CompositeFactorySpecBuilders.Reasons;

public readonly ref struct FalseReasonsWithDescriptionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause,
    string candidateName)
{
    public ReasonWithDescriptionCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(specPredicate,
            trueBecause,
            _ => falseBecause,
            candidateName);

    public ReasonWithDescriptionCompositeFactorySpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            falseBecause,
            candidateName);
}