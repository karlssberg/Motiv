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
    
    public static IEnumerable<TBooleanResult> WhereTrue<TBooleanResult>(
        this IEnumerable<TBooleanResult> results)
        where TBooleanResult : BooleanResultBase =>
        results.Where(result => result.Satisfied);
    
    public static IEnumerable<TBooleanResult> WhereFalse<TBooleanResult>(
        this IEnumerable<TBooleanResult> results)
        where TBooleanResult : BooleanResultBase =>
        results.Where(result => !result.Satisfied);

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

    internal static Explanation CreateExplanation(
        this IEnumerable<BooleanResultBase> underlyingResults)
    {
        var resultArray = underlyingResults.ToArray();

        var reasons = resultArray
            .SelectMany(result => result.Explanation.Assertions)
            .Distinct();

        var underlying = resultArray
            .Select(result => result.Explanation);

        return new Explanation(reasons)
        {
            Underlying = underlying
        };
    }
    
    public static IEnumerable<string> GetAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results.SelectMany(e => e.Assertions);
    
    public static IEnumerable<string> GetTrueAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .Where(r => r.Satisfied)
            .SelectMany(e => e.Assertions);
    
    public static IEnumerable<string> GetFalseAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .Where(r => !r.Satisfied)
            .SelectMany(e => e.Assertions);
    
    
    public static IEnumerable<TMetadata> GetMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.SelectMany(e => e.MetadataTree);
    
    public static IEnumerable<TMetadata> GetTrueMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results
            .Where(r => r.Satisfied)
            .SelectMany(e => e.MetadataTree);
    
    public static IEnumerable<TMetadata> GetFalseMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results
            .Where(r => !r.Satisfied)
            .SelectMany(e => e.MetadataTree);
    
    public static IEnumerable<string> GetAssertions(
        this IEnumerable<Explanation> explanations) =>
        explanations.SelectMany(e => e.Assertions);
    
    public static IEnumerable<string> GetAssertionsAtDepth(
        this Explanation explanation,
        int atDepth) =>
        atDepth switch
        {
            > 0 => explanation.Underlying.GetAssertionsAtDepth(atDepth - 1).Distinct(),
            0 => explanation.Assertions,
            _ => throw new ArgumentOutOfRangeException(nameof(atDepth), "Depth must be a non-negative integer.")
        };
    
    private static IEnumerable<string> GetAssertionsAtDepth(
        this IEnumerable<Explanation> explanations,
        int atDepth) =>
        atDepth switch
        {
            > 0 => explanations.GetAssertionsAtDepth(atDepth - 1),
            0 => explanations.SelectMany(e => e.Assertions),
            _ => throw new ArgumentOutOfRangeException(nameof(atDepth), "Depth must be a non-negative integer.")
        };
    
    public static IEnumerable<string> GetRootAssertions(
        this BooleanResultBase result) =>
        result.Explanation
            .Underlying
            .GetRootAssertions()
            .Distinct();
    
    private static IEnumerable<string> GetRootAssertions(
        this IEnumerable<Explanation> explanations) =>
        explanations.SelectMany(explanation => explanation
            .Underlying
            .GetRootAssertions()
            .ElseIfEmpty(explanation.Assertions));

    internal static IEnumerable<T> ElseIfEmpty<T>(
        this IEnumerable<T> source,
        IEnumerable<T> alternative)
    {
        using var sourceEnumerator = source.GetEnumerator();
        var sourceHasItems = sourceEnumerator.MoveNext();
        
        if (sourceHasItems)
            yield return sourceEnumerator.Current;
        
        while (sourceEnumerator.MoveNext())
            yield return sourceEnumerator.Current;
        
        if (sourceHasItems)
            yield break;
        
        using var alternativeEnumerator = alternative.GetEnumerator();
        while (alternativeEnumerator.MoveNext())
            yield return alternativeEnumerator.Current;
    }

    public static IEnumerable<T> ToEnumerable<T>(this T item)
    {
        yield return item;
    }

    internal static bool HasAtLeast<T>(this IEnumerable<T> source, int n)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        using var enumerator = source.GetEnumerator();
        
        for (var i = 0; i < n; i++)
            if (!enumerator.MoveNext())
                return false;

        return true;
    }
}

