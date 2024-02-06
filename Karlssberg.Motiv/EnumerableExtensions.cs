namespace Karlssberg.Motiv;

/// <summary>Provides extension methods for working with enumerable collections.</summary>
public static class EnumerableExtensions
{
    public static SpecBase<TModel, TMetadata> AndTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specifications) =>
        specifications.Aggregate((leftSpec, rightSpec) => leftSpec & rightSpec);

    public static SpecBase<TModel, TMetadata> OrTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specifications) =>
        specifications.Aggregate((leftSpec, rightSpec) => leftSpec | rightSpec);
    
    /// <summary>Determines whether any element in the collection satisfies the specified specification.</summary>
    /// <typeparam name="TModel">The type of the elements in the collection.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
    /// <param name="source">The collection to check.</param>
    /// <param name="spec">The specification to apply.</param>
    /// <returns>A BooleanResultBase indicating whether any element satisfies the specification.</returns>
    public static BooleanResultBase<TMetadata> Any<TModel, TMetadata>(
        this IEnumerable<TModel> source,
        SpecBase<TModel, TMetadata> spec) =>
        spec.Any().IsSatisfiedBy(source);

    /// <summary>Determines whether all elements in the collection satisfy the specified specification.</summary>
    /// <typeparam name="TModel">The type of the elements in the collection.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata associated with the specification.</typeparam>
    /// <param name="source">The collection to check.</param>
    /// <param name="spec">The specification to apply.</param>
    /// <returns>A BooleanResultBase indicating whether all elements satisfy the specification.</returns>
    public static BooleanResultBase<TMetadata> All<TModel, TMetadata>(
        this IEnumerable<TModel> source,
        SpecBase<TModel, TMetadata> spec) =>
        spec.Any().IsSatisfiedBy(source);
    
    /// <summary>Returns the source collection if it is not empty; otherwise, returns the specified alternative collection.</summary>
    /// <typeparam name="T">The type of the elements in the collections.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="other">The alternative collection.</param>
    /// <returns>The source collection if it is not empty; otherwise, the alternative collection.</returns>
    internal static IEnumerable<T> IfEmptyThen<T>(this IEnumerable<T> source, IEnumerable<T> other)
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

    internal static (IEnumerable<T> first, IEnumerable<T> second) Partition<T>(this IEnumerable<T> enumerable,
        Func<T, bool> predicate)
    {
        var trueList = new List<T>();
        var falseList = new List<T>();
        foreach (var item in enumerable)
        {
            var list = predicate(item)
                ? trueList
                : falseList;

            list.Add(item);
        }

        return (trueList, falseList);
    }
}