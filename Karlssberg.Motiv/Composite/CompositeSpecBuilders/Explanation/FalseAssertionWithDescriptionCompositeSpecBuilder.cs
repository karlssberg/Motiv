namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;

public readonly ref struct FalseAssertionWithDescriptionCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    string candidateProposition)
{
    public ExplanationWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        string falseBecause) =>
        new(spec,
            trueBecause,
            (_, _) => falseBecause.ToEnumerable(),
            candidateProposition);

    public ExplanationWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            (model, _) => falseBecause(model).ToEnumerable(),
            candidateProposition);
    
    public ExplanationWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            trueBecause,
            (model, result) => falseBecause(model, result).ToEnumerable(),
            candidateProposition);
    
    public ExplanationWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause,
            candidateProposition);
}