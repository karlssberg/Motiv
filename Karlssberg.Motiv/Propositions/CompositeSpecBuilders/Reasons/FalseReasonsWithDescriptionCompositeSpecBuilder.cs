namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly ref struct FalseReasonsWithDescriptionCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    string candidateProposition)
{
    public ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            trueBecause,
            _ => falseBecause,
            candidateProposition);

    public ReasonWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause,
            candidateProposition);
}