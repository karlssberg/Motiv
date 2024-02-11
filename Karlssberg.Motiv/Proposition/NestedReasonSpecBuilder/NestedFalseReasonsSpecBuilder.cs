namespace Karlssberg.Motiv.Proposition.NestedReasonSpecBuilder;

public readonly struct NestedFalseReasonsSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause)
{
    public NestedReasonSpecFactory<TModel, TUnderlyingMetadata> YieldWhenFalse(string falseBecause) =>
        new(specPredicate,
            trueBecause,
            _ => falseBecause);

    public NestedReasonSpecFactory<TModel, TUnderlyingMetadata> YieldWhenFalse(Func<TModel, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            falseBecause);
}