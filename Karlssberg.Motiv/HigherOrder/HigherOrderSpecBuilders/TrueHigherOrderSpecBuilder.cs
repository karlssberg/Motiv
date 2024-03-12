using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

public readonly ref struct TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector = null)
{
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) => 
        new(spec,
            higherOrderPredicate, _ => whenTrue.ToEnumerable(),
            causeSelector);
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            results => whenTrue(results).ToEnumerable(),
            causeSelector);
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    public FalseAssertionsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec,
            higherOrderPredicate,
            _ => trueBecause.ToEnumerable(),
            trueBecause,
            AssertionSource.Metadata,
            causeSelector);
    
    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            results => trueBecause(results).ToEnumerable(),
            AssertionSource.Metadata,
            causeSelector);

    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            AssertionSource.Proposition,
            causeSelector);
    
    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string proposition) =>
        new HigherOrderSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            _ => proposition.ToEnumerable(),
            _ => $"!{proposition}".ToEnumerable(),
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            AssertionSource.Proposition,
            causeSelector);
}
