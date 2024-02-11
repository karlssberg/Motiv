namespace Karlssberg.Motiv.Proposition.NestedMetadataSpecBuilder;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata"></typeparam>
public readonly struct NestedTrueSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate)
{
    public NestedFalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(specPredicate, _ => whenTrue);

    public NestedFalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue) =>
        new(specPredicate, whenTrue);
    
    public NestedFalseReasonsWithDescriptionSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(specPredicate, _ => trueBecause, trueBecause);

    public NestedFalseReasonsSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
        new(specPredicate, trueBecause);
}