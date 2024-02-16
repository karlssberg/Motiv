using Karlssberg.Motiv.Proposition.HigherOrderSpecBuilders.Metadata;
using Karlssberg.Motiv.Proposition.HigherOrderSpecBuilders.Reasons;
using Karlssberg.Motiv.Proposition.SpecBuilders.Metadata;
using Karlssberg.Motiv.Proposition.SpecBuilders.Reasons;

namespace Karlssberg.Motiv.Proposition.HigherOrderSpecBuilders;

public readonly struct TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate)
{
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec, higherOrderPredicate, _ => [whenTrue]);
    
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec, higherOrderPredicate, results => [whenTrue(results)]);
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec, higherOrderPredicate, whenTrue);

    public FalseReasonsWithDescriptionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec, higherOrderPredicate, _ => [trueBecause], trueBecause);
    
    public FalseReasonsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec, higherOrderPredicate, results => [trueBecause(results)]);

    public FalseReasonsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<string>> trueBecause) =>
        new(spec, higherOrderPredicate, trueBecause);
}
