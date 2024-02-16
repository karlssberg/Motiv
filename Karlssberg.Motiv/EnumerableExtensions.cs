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
    
    public static IEnumerable<string> GetReasonsAtLevel(
        this IEnumerable<Reason> reasons,
        int level) => 
            level switch 
            {
                0 => reasons.Select(reason => reason.Description),
                _ => reasons
                    .SelectMany(reason => reason.UnderlyingReasons)
                    .GetReasonsAtLevel(level - 1)
            };

    public static IEnumerable<string> GetRootCauseReasons(this IEnumerable<Reason> reasons)
    {
        while (true)
        {
            var reasonsArray = reasons.ToArray();
            var underlyingReasons = reasonsArray.SelectMany(r => r.UnderlyingReasons).ToArray();
            if (!underlyingReasons.Any()) 
                return reasonsArray.Select(r => r.Description);
            
            reasons = underlyingReasons;
        }
    }

    public static int CountTrue<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Count(result => result.Satisfied);
    
    public static int CountFalse<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Count(result => !result.Satisfied);
    
    public static bool AllTrue<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.All(result => result.Satisfied);
    
    public static bool AllFalse<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.All(result => !result.Satisfied);
    
    public static bool AnyTrue<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Any(result => result.Satisfied);
    
    public static bool AnyFalse<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Any(result => !result.Satisfied);
    
    
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