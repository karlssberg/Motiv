namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;

public readonly ref struct FalseMultiAssertionCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause)
{
    public ExplanationMultiAssertionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        string falseBecause) =>
        new(spec,
            trueBecause,
            (_, _) => falseBecause.ToEnumerable());

    public ExplanationMultiAssertionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, string> falseBecause) =>
        new(spec,
            trueBecause,
            (model, _) => falseBecause(model).ToEnumerable());
    
    public ExplanationMultiAssertionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause.ToEnumerableReturn());
    
    public ExplanationMultiAssertionCompositeSpecFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(spec,
            trueBecause,
            falseBecause);
}