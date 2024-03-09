using Karlssberg.Motiv.CompositeFactory;

namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;

public readonly ref struct ExplanationWithDescriptionCompositeSpecFactory<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> falseBecause,
    string candidateProposition)
{
    public SpecBase<TModel, string> CreateSpec() =>
        CreateSpecInternal(candidateProposition);
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        CreateSpecInternal(proposition.ThrowIfNullOrWhitespace(nameof(proposition)));

    private SpecBase<TModel, string> CreateSpecInternal(string proposition) =>
        new CompositeSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            trueBecause,
            falseBecause,
            proposition);
}