using Karlssberg.Motiv.Proposition.Factories;
using Karlssberg.Motiv.Proposition.YieldWhenFalse;

namespace Karlssberg.Motiv.Proposition;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
internal class NestedTrueMetadataSpecBuilder<TModel, TMetadata>(Func<TModel, SpecBase<TModel, TMetadata>> specPredicate) : 
    INestedMetadataSpecBuilderStart<TModel, TMetadata>, 
    IDescriptiveSpecFactory<TModel, TMetadata>
{
    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue(TMetadata whenTrue) =>
        new NestedFalseMetadataSpecBuilder<TModel, TMetadata>(specPredicate, _ => whenTrue);

    public IYieldMetadataWhenFalse<TModel, TMetadata> YieldWhenTrue(Func<TModel, TMetadata> whenTrue) => 
        new NestedFalseMetadataSpecBuilder<TModel, TMetadata>(specPredicate, whenTrue);

    public SpecBase<TModel, TMetadata> CreateSpec(string description) =>
        new Spec<TModel, TMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            specPredicate);
}