using System.Collections;

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
    
    internal static CausalMetadataCollection<TMetadata> CreateCause<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> causalResults)
    {
        var causalResultsArray = causalResults.ToArray();
        var metadata = causalResultsArray
            .SelectMany(cause => cause.CausalMetadata.Metadata);
        
        var reasons = causalResultsArray
            .SelectMany(cause => cause.Explanation.Assertions);
        
        var underlyingCauses = causalResultsArray
            .SelectMany(cause => cause.CausalMetadata.Underlying);
        
        return new CausalMetadataCollection<TMetadata>(metadata, reasons)
        {
            Underlying = underlyingCauses
        };
    }
    
    internal static Explanation CreateReason(
        this IEnumerable<BooleanResultBase> underlyingResults)
    {
        var resultArray = underlyingResults.ToArray();

        var reasons = resultArray
            .SelectMany(result => result.Explanation.Assertions)
            .Distinct()
            .OrderBy(d => d);
        
        var underlying = resultArray
            .Select(result => result.Explanation);
        
        return new Explanation(reasons)
        {
            Underlying = underlying
        };
    }
    
    internal static IEnumerable<T> ToEnumerable<T>(this T item)
    {
        yield return item;
    }
    internal static IEnumerable<T> ToEnumerable<T>(this IEnumerable<T> item) => item;
}