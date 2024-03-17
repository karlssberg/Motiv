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

    
    
    public FalseAssertionsWithPropositionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec,
            higherOrderPredicate,
            _ => trueBecause,
            trueBecause,
            causeSelector);
    
    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            causeSelector);
    
    public SpecBase<IEnumerable<TModel>, string> CreateSpec(string proposition) =>
        new HigherOrderMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            _ => proposition,
            _ => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            causeSelector);
}
