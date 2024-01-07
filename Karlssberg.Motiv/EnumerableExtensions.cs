namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for working with enumerable collections.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Determines whether any element in the collection satisfies the specified specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the elements in the collection.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
    /// <param name="source">The collection to check.</param>
    /// <param name="specification">The specification to apply.</param>
    /// <returns>A BooleanResultBase indicating whether any element satisfies the specification.</returns>
    public static BooleanResultBase<TMetadata> Any<TModel, TMetadata>(
        this IEnumerable<TModel> source,
        SpecificationBase<TModel, TMetadata> specification)
    {
        return specification.ToAnySatisfiedSpec().IsSatisfiedBy(source);
    }

    /// <summary>
    /// Determines whether all elements in the collection satisfy the specified specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the elements in the collection.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
    /// <param name="source">The collection to check.</param>
    /// <param name="specification">The specification to apply.</param>
    /// <returns>A BooleanResultBase indicating whether all elements satisfy the specification.</returns>
    public static BooleanResultBase<TMetadata> All<TModel, TMetadata>(
        this IEnumerable<TModel> source,
        SpecificationBase<TModel, TMetadata> specification)
    {
        return specification.ToAnySatisfiedSpec().IsSatisfiedBy(source);
    }

    /// <summary>
    /// Combines multiple specifications into a single specification that requires all of them to be satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the elements in the collection.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the specifications.</typeparam>
    /// <param name="specifications">The specifications to combine.</param>
    /// <returns>A SpecificationBase that requires all of the input specifications to be satisfied.</returns>
    public static SpecificationBase<TModel, TMetadata> ToAllSatisfiedSpec<TModel, TMetadata>(
        this IEnumerable<SpecificationBase<TModel, TMetadata>> specifications)
    {
        return specifications.Aggregate(
            (leftSpec, rightSpec) =>
                leftSpec & rightSpec);
    }

    /// <summary>
    /// Combines multiple specifications into a single specification that requires any of them to be satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the elements in the collection.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the specifications.</typeparam>
    /// <param name="specifications">The specifications to combine.</param>
    /// <returns>A SpecificationBase that requires any of the input specifications to be satisfied.</returns>
    public static SpecificationBase<TModel, TMetadata> ToAnySatisfiedSpec<TModel, TMetadata>(
        this IEnumerable<SpecificationBase<TModel, TMetadata>> specifications)
    {
        return specifications.Aggregate(
            (leftSpec, rightSpec) =>
                leftSpec | rightSpec);
    }

    /// <summary>
    /// Returns the source collection if it is not empty; otherwise, returns the specified alternative collection.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collections.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="other">The alternative collection.</param>
    /// <returns>The source collection if it is not empty; otherwise, the alternative collection.</returns>
    public static IEnumerable<T> IfEmptyThen<T>(this IEnumerable<T> source, IEnumerable<T> other)
    {
        var hasItems = false;
        foreach (var item in source)
        {
            yield return item;
            hasItems = true;
        }

        if (hasItems)
            yield break;

        foreach (var item in other)
            yield return item;
    }
}