﻿namespace Karlssberg.Motiv;

/// <summary>Provides extension methods for working with enumerable collections.</summary>
public static class EnumerableExtensions
{
    public static SpecBase<TModel, TMetadata> AndTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specifications) =>
        specifications.Aggregate((leftSpec, rightSpec) => leftSpec & rightSpec);

    public static SpecBase<TModel, TMetadata> OrTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specifications) =>
        specifications.Aggregate((leftSpec, rightSpec) => leftSpec | rightSpec);

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
    
    internal static Cause<TMetadata> CreateCause<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    {
        var causalResultsArray = causalResults.ToArray();
        var metadata = causalResultsArray
            .SelectMany(cause => cause.Cause.Metadata);
        
        var reasons = causalResultsArray
            .SelectMany(cause => cause.Cause.Reasons);
        
        var underlyingCauses = causalResultsArray
            .SelectMany(cause => cause.Cause.Underlying);
        
        return new Cause<TMetadata>(metadata, reasons)
        {
            Underlying = underlyingCauses
        };
    }
    
    internal static Explanation CreateExplanation(
        this IEnumerable<BooleanResultBase> underlyingResults)
    {
        var resultArray = underlyingResults.ToArray();

        var reasons = resultArray
            .SelectMany(result => result.Explanation.Reasons);
        
        var underlying = resultArray
            .Select(result => result.Explanation);
        
        return new Explanation(reasons)
        {
            Underlying = underlying
        };
    }
    
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

    internal static IEnumerable<T> ModifyFirst<T>(this IEnumerable<T> source, Func<T, T> modifier)
    {
        var modified = false;
        foreach (var item in source)
        {
            if (!modified)
            {
                modified = true;
                yield return modifier(item);
            }
            else
            {
                yield return item;
            }
        }
    }
}