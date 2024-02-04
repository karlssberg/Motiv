namespace Karlssberg.Motiv.Proposition;

/// <summary>Represents a builder for creating specifications based on a predicate.</summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata"></typeparam>
internal class NestedSpecBuilder<TModel, TMetadata>(Func<TModel, SpecBase<TModel, TMetadata>> specPredicate) : 
    IDescribeSpec<TModel, TMetadata>
{
    public SpecBase<TModel, TMetadata> WithDescription(string description) =>
        new Spec<TModel, TMetadata>(
            description.ThrowIfNullOrWhitespace(nameof(description)),
            specPredicate);
}

public interface IDescribeSpec<TModel, TMetadata>
{
    public SpecBase<TModel, TMetadata> WithDescription(string description);   
}
