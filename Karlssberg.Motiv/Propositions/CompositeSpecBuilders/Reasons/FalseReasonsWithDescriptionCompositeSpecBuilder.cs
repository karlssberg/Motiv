namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly struct FalseReasonsWithDescriptionCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    string candidateDescription)
{
    public ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            trueBecause,
            _ => falseBecause,
            candidateDescription);

    public ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause,
            candidateDescription);
}