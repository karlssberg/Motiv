using Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;
using Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Metadata;

namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata"></typeparam>
public readonly ref struct TrueCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate)
{
    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(specPredicate,
            (_, _) => whenTrue.ToEnumerable());

    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(specPredicate,
            (model, _) => whenTrue(model).ToEnumerable());

    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(specPredicate,
            (model, result) => whenTrue(model, result).ToEnumerable());

    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(specPredicate,
            whenTrue);
    
    public FalseAssertionWithPropositionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) =>
        new(specPredicate,
            (_, _) => trueBecause,
            trueBecause);

    public FalseAssertionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, string> trueBecause) =>
        new(specPredicate,
            (model, _) => trueBecause(model));

    public FalseAssertionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause) =>
        new(specPredicate,
            trueBecause);
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new CompositeFactoryMetadataSpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            (_, _) => proposition,
            (_, _) => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}