namespace Karlssberg.Motiv.Proposition.SpecBuilders.Reasons;

public readonly struct FalseReasonsWithDescriptionSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause,
    string candidateDescription)
{
    public ReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            trueBecause,
            _ => falseBecause,
            candidateDescription);

    public ReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause,
            candidateDescription);
}