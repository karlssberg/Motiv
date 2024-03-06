using Karlssberg.Motiv.HigherOrder;
using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Explanation;
using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

public readonly ref struct TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)
{
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec,
            higherOrderPredicate, _ => whenTrue.ToEnumerable());
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            results => whenTrue(results).ToEnumerable());
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            whenTrue);

    public FalseAssertionsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec,
            higherOrderPredicate,
            _ => trueBecause.ToEnumerable(),
            trueBecause,
            ReasonSource.Metadata);
    
    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            results => trueBecause(results).ToEnumerable(),
            ReasonSource.Metadata);

    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            ReasonSource.Proposition);
    
    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string proposition) =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            _ => proposition.ToEnumerable(),
            _ => $"!{proposition}".ToEnumerable(),
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            ReasonSource.Proposition);
}
