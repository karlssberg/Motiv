namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;

public readonly ref struct ExplanationMultiAssertionWithPropositionCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    string candidateProposition)
{
    public SpecBase<TModel, string> Create(string proposition) =>
        new CompositeMultiMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
    
    public SpecBase<TModel, string> Create() =>
        new CompositeMultiMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            candidateProposition);
}