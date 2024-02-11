namespace Karlssberg.Motiv.Proposition.NestedReasonSpecBuilder;

public readonly struct NestedFalseReasonsWithDescriptionSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate,
    Func<TModel, string> trueBecause,
    string candidateDescription)
{
    public NestedReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(specPredicate,
            trueBecause,
            _ => falseBecause,
            candidateDescription);

    public NestedReasonWithDescriptionSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(Func<TModel, string> falseBecause) =>
        new(specPredicate,
            trueBecause,
            falseBecause,
            candidateDescription);
}