using Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Explanation;
using Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders.Metadata;

namespace Karlssberg.Motiv.CompositeFactory.CompositeFactorySpecBuilders;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata"></typeparam>
public readonly ref struct TrueCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate)
{
    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(specPredicate,
            _ => whenTrue);

    public FalseMetadataCompositeSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new(specPredicate,
            whenTrue);
    
    public FalseAssertionWithDescriptionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(specPredicate,
            _ => trueBecause,
            trueBecause);

    public FalseAssertionCompositeFactorySpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
        new(specPredicate,
            trueBecause);
    
    public SpecBase<TModel, string> CreateSpec(string proposition) =>
        new CompositeFactorySpec<TModel, string, TUnderlyingMetadata>(
            specPredicate,
            _ => proposition,
            _ => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}