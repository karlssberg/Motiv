namespace Karlssberg.Motiv.Proposition.SpecBuilders;

public readonly struct FalseReasonsSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, string> trueBecause)
{
    public ReasonSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(spec,
            trueBecause,
            _ => falseBecause);

    public ReasonSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause);
}