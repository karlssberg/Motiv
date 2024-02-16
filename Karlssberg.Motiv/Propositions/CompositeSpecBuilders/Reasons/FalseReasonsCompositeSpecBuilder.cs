namespace Karlssberg.Motiv.Propositions.CompositeSpecBuilders.Reasons;

public readonly struct FalseReasonsCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause)
{
    public ReasonCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            trueBecause,
            _ => falseBecause);

    public ReasonCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause);
}