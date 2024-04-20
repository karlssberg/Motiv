namespace Karlssberg.Motiv;

/// <summary>Provides extension methods for working with enumerable collections.</summary>
public static class EnumerableExtensions
{
    public static SpecBase<TModel, TMetadata> AndTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec & rightSpec);
    public static BooleanResultBase<TMetadata> AndTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResults) =>
        booleanResults.Aggregate((leftSpec, rightSpec) => leftSpec & rightSpec);
    
    public static SpecBase<TModel, TMetadata> AndAlsoTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec.AndAlso(rightSpec));
    public static BooleanResultBase<TMetadata> AndAlsoTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResults) =>
        booleanResults.Aggregate((leftSpec, rightSpec) => leftSpec.AndAlso(rightSpec));

    public static SpecBase<TModel, TMetadata> OrTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec | rightSpec);
    
    public static BooleanResultBase<TMetadata> OrTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResult) =>
        booleanResult.Aggregate((leftSpec, rightSpec) => leftSpec | rightSpec);
    
    public static SpecBase<TModel, TMetadata> OrElseTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec.OrElse(rightSpec));
    
    public static BooleanResultBase<TMetadata> OrElseTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResult) =>
        booleanResult.Aggregate((leftSpec, rightSpec) => leftSpec.OrElse(rightSpec));
    
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
        this IEnumerable<BooleanResultBase> causes)
    {
        var causeArray = causes.ToArray();
        var assertions = causeArray.GetAssertions();

        return new Explanation(assertions, causeArray);
    }
    
    internal static IEnumerable<Explanation> GetExplanations(this IEnumerable<BooleanResultBase> results) =>
        results.Select(result => result.Explanation);

    public static IEnumerable<string> GetAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results
            .SelectMany(result =>
                result switch
                {
                    IBooleanOperationResult operationResult => operationResult.Causes.GetAssertions(),
                    _ => result.Assertions
                });
    
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
        results.SelectMany(e => e.Metadata);
    
    public static IEnumerable<TMetadata> GetTrueMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results
            .Where(r => r.Satisfied)
            .SelectMany(e => e.Metadata);
    
    public static IEnumerable<TMetadata> GetFalseMetadata<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results
            .Where(r => !r.Satisfied)
            .SelectMany(e => e.Metadata);
    
    public static IEnumerable<string> GetAssertions(
        this IEnumerable<Explanation> explanations) =>
        explanations.SelectMany(e => e.Assertions).Distinct();
    
    public static IEnumerable<string> GetAssertionsAtDepth(
        this BooleanResultBase result,
        int atDepth) =>
        atDepth switch
        {
            0 => result.Assertions,
            > 0 => result.Underlying.GetAssertionsAtDepth(atDepth - 1).Distinct(),
            _ => throw new ArgumentOutOfRangeException(nameof(atDepth), "Depth must be a non-negative integer.")
        };
    
    private static IEnumerable<string> GetAssertionsAtDepth(
        this IEnumerable<BooleanResultBase> results,
        int atDepth) =>
        atDepth switch
        {
            0 => results.SelectMany(e => e.Assertions),
            > 0 => results.GetAssertionsAtDepth(atDepth - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(atDepth), "Depth must be a non-negative integer.")
        };
    
    public static IEnumerable<string> GetAllAssertionsAtDepth(
        this BooleanResultBase result,
        int atDepth) =>
        atDepth switch
        {
            0 => result.AllAssertions,
            > 0 => result.AggregateBinaryOperationResults().GetAllAssertionsAtDepth(atDepth - 1).Distinct(),
            _ => throw new ArgumentOutOfRangeException(nameof(atDepth), "Depth must be a non-negative integer.")
        };
    
    private static IEnumerable<string> GetAllAssertionsAtDepth(
        this IEnumerable<BooleanResultBase> results,
        int atDepth) =>
        results.SelectMany(result => result.GetAllAssertionsAtDepth(atDepth));
    
    public static IEnumerable<string> GetRootAssertions(
        this BooleanResultBase result) =>
        result.Explanation
            .Underlying
            .GetRootAssertions()
            .Distinct()
            .ElseIfEmpty(result.Assertions);
    
    private static IEnumerable<string> GetRootAssertions(
        this IEnumerable<Explanation> explanations) =>
        explanations.SelectMany(explanation => explanation
            .Underlying
            .GetRootAssertions()
            .ElseIfEmpty(explanation.Assertions));
    
    public static IEnumerable<string> GetAllRootAssertions(
        this BooleanResultBase result) =>
        result.Underlying
            .GetAllRootAssertions()
            .Distinct()
            .ElseIfEmpty(result.Assertions);
    
    private static IEnumerable<string> GetAllRootAssertions(
        this IEnumerable<BooleanResultBase> results) =>
        results.SelectMany(result => result
            .GetAllRootAssertions()
            .ElseIfEmpty(result.Assertions));
    
    internal static IEnumerable<TMetadata> GetRootMetadata<TMetadata>(
        this BooleanResultBase<TMetadata> result) =>
        result.MetadataTier
            .Underlying
            .GetRootMetadata()
            .Distinct();
    
    private static IEnumerable<TMetadata> GetRootMetadata<TMetadata>(
        this IEnumerable<MetadataNode<TMetadata>> metadataTrees) =>
        metadataTrees.SelectMany(MetadataTier => MetadataTier
            .Underlying
            .GetRootMetadata()
            .ElseIfEmpty(MetadataTier.Metadata));
    
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

    private static IEnumerable<BooleanResultBase> AggregateUnderlyingCauses(
        this IEnumerable<BooleanResultBase> underlyingResult,
        int atDepth = 0) =>
        underlyingResult.SelectMany(result => AggregateUnderlyingCauses(result,atDepth));

    private static IEnumerable<BooleanResultBase> AggregateUnderlyingCauses(
        this BooleanResultBase result,
        int atDepth = 0) =>
        result switch
        {
            IBinaryBooleanOperationResult => result.Causes.AggregateUnderlyingCauses(atDepth),
            _ when atDepth > 0 => result.Causes.AggregateUnderlyingCauses(atDepth - 1),
            _ => result.ToEnumerable()
        };
    
    internal static IEnumerable<BooleanResultBase> AggregateBinaryOperationResults(
        this IEnumerable<BooleanResultBase> results) =>
        results.SelectMany(AggregateBinaryOperationResults);

    private static IEnumerable<BooleanResultBase> AggregateBinaryOperationResults(
        this BooleanResultBase result,
        int atDepth = 0) =>
        result.Underlying.SelectMany(underlyingResult => 
            underlyingResult switch
            {
                IBinaryBooleanOperationResult => underlyingResult.AggregateBinaryOperationResults(atDepth),
                _ when atDepth > 0 => underlyingResult.AggregateBinaryOperationResults(atDepth - 1),
                _ => result.Underlying,
            });

    private static IEnumerable<BooleanResultBase<TMetadata>> AggregateBinaryOperationResults<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.SelectMany(AggregateBinaryOperationResults);

    private static IEnumerable<BooleanResultBase<TMetadata>> AggregateBinaryOperationResults<TMetadata>(
        this BooleanResultBase<TMetadata> result) =>
        result switch
        {
            IBinaryBooleanOperationResult => result.UnderlyingWithMetadata.AggregateBinaryOperationResults(),
            _ => result.UnderlyingWithMetadata
        };
}

