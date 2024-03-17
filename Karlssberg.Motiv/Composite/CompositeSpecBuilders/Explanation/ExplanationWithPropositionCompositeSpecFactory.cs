namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;

public readonly ref struct ExplanationWithPropositionCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string candidateProposition)
{
    public SpecBase<TModel, string> CreateSpec() =>
        new CompositeExplanationSpec<TModel,TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            candidateProposition);
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new CompositeExplanationSpec<TModel, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));

}