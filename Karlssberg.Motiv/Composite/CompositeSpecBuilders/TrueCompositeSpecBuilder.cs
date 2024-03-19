using Karlssberg.Motiv.Composite.CompositeSpecBuilders.Explanation;
using Karlssberg.Motiv.Composite.CompositeSpecBuilders.Metadata;
using Karlssberg.Motiv.HigherOrder;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.Composite.CompositeSpecBuilders;

public readonly ref struct TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
{
    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec, (_, _) => whenTrue.ToEnumerable());

    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(spec, (model, _) => whenTrue(model).ToEnumerable());
    
    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec, (model, result) => whenTrue(model, result).ToEnumerable());
    
    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec, whenTrue);

    public FalseAssertionWithPropositionCompositeSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec, (_, _) => trueBecause, trueBecause);

    public FalseAssertionCompositeSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
        new(spec, (model, _) => trueBecause(model));
    
    public FalseAssertionCompositeSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause) =>
        new(spec, trueBecause);
    
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        new(spec, higherOrderPredicate);
    public TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector) =>
        new(spec, higherOrderPredicate, causeSelector);
    
    public SpecBase<TModel, string> Create(string proposition) =>
        new CompositeMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            (_, _) => proposition,
            (_, _) => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}