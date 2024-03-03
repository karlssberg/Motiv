using Karlssberg.Motiv.HigherOrder;
using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Explanation;
using Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.Propositions.HigherOrderSpecBuilders;

public readonly ref struct TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)
{
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec,
            higherOrderPredicate, _ => whenTrue.ToEnumerable());
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            results => whenTrue(results).ToEnumerable());
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
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
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            results => trueBecause(results).ToEnumerable(),
            ReasonSource.Metadata);

    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            ReasonSource.Proposition);
}
