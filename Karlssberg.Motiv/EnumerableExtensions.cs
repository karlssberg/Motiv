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
        var causeCollection = causes as ICollection<BooleanResultBase> ?? causes.ToArray();
        var assertions = causeCollection.GetAssertions();

        return new Explanation(assertions, causeCollection);
    }
    
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
        this IEnumerable<MetadataNode<TMetadata>> results) =>
        results.SelectMany(e => e.Metadata);
    
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
        this IEnumerable<MetadataNode<TMetadata>> metadataTiers) =>
        metadataTiers.SelectMany(metadataTier => metadataTier
            .Underlying
            .GetRootMetadata()
            .ElseIfEmpty(metadataTier.Metadata));
    
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

    public static IEnumerable<T> ToEnumerable<T>(this T? item)
    {
        if (item is null) yield break;
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
    
    public static IEnumerable<string> SurroundWithBrackets(this IEnumerable<string> lines)
    {
        yield return "(";
        foreach (var line in lines)
            yield return line.IndentLine();
        yield return ")";
    }
    
    public static IEnumerable<string> ReplaceFirstLine(this IEnumerable<string> lines, Func<string, string> prefixFn)
    {
        using var enumerator = lines.GetEnumerator();
        if (!enumerator.MoveNext())
            yield break;

        var firstLine = enumerator.Current;
        yield return prefixFn(firstLine);

        while (enumerator.MoveNext())
            yield return enumerator.Current ?? "";
    }
    
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source) =>
        source.Select((item, index) => (item, index));
    
    public static IEnumerable<string> GetBinaryDetailsAsLines(
        this IEnumerable<BooleanResultBase> causalResults,
        string operation,
        int level = 0)
    {
        var adjacentLineGroups = causalResults
            .IdentifyCollapsible(operation)
            .Select(GetDetailsAsTuple)
            .GroupAdjacentBy((prev, next) => prev.op == next.op)
            .SelectMany(group =>
            {
                var groupArray = group.ToArray();
                if (groupArray.Length == 0)
                    return Enumerable.Empty<(OperationGroup op, IEnumerable<string> detailsAsLines)>();

                var op = groupArray.First().op;
                var detailsAsLines = groupArray.SelectMany(tuple => tuple.detailsAsLines);
                return (op, detailsAsLines).ToEnumerable();
            });
        
        foreach (var group in adjacentLineGroups)
        {
            if (group.op != OperationGroup.Collapsible && level == 0)
                yield return operation;
            
            foreach (var line in group.detailsAsLines)
                yield return line.IndentLine();
        }
        
        yield break;

        (OperationGroup op, IEnumerable<string> detailsAsLines) GetDetailsAsTuple((OperationGroup, BooleanResultBase) tuple)
        {
            var (op, result) = tuple;
            var detailsAsLines = result switch
            {
                IBinaryBooleanOperationResult binaryOperationResult
                    when binaryOperationResult.Operation == operation
                         && binaryOperationResult.IsCollapsable =>
                    result.ToEnumerable().GetBinaryDetailsAsLines(operation, level + 1),
                _ =>
                    result.Description.GetDetailsAsLines()
            };

            return (op, detailsAsLines);
        }
    }
    
    internal static IEnumerable<(OperationGroup, BooleanResultBase)> IdentifyCollapsible(
        this IEnumerable<BooleanResultBase> results,
        string operation)
    {
        return results.SelectMany(result =>
            result switch
            {
                IBinaryBooleanOperationResult binaryOperationResults
                    when binaryOperationResults.Operation == operation
                         && binaryOperationResults.IsCollapsable =>
                    IdentifyCollapsible(binaryOperationResults, operation),
                _ =>
                    (otherResults: OperationGroup.Other, result).ToEnumerable()
            });
    }

    internal static IEnumerable<(OperationGroup, BooleanResultBase)> IdentifyCollapsible(
        this IBinaryBooleanOperationResult binaryResult,
        string operation)
    {
        var underlyingResults = binaryResult.Causes;
    
        return underlyingResults
            .SelectMany(underlyingResult => 
                underlyingResult switch
                {
                    IBinaryBooleanOperationResult binaryOperationResult
                        when binaryOperationResult.Operation == operation
                             && binaryOperationResult.IsCollapsable =>
                        binaryOperationResult.Causes.IdentifyCollapsible(operation),
                    _ =>
                        (otherResults: OperationGroup.Other, underlyingResult).ToEnumerable()
                });
    }

    public static IEnumerable<string> GetBinaryDetailsAsLines<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specs,
        string operation,
        int level  = 0)
    {
        var adjacentLineGroups = specs
            .IdentifyCollapsible(operation)
            .Select(GetDetailsAsTuple)
            .GroupAdjacentBy((prev, next) => prev.op == next.op)
            .SelectMany(group =>
            {
                var groupArray = group.ToArray();
                if (groupArray.Length == 0)
                    return Enumerable.Empty<(OperationGroup op, IEnumerable<string> detailsAsLines)>();

                var op = groupArray.First().op;
                var detailsAsLines = groupArray.SelectMany(tuple => tuple.detailsAsLines);
                return (op, detailsAsLines).ToEnumerable();
            });
        
        
        foreach (var group in adjacentLineGroups)
        {
            if (group.op != OperationGroup.Collapsible && level == 0)
            {
                yield return operation;
            }
            
            foreach (var line in group.detailsAsLines)
                yield return line.IndentLine();
        }
        
        yield break;

        (OperationGroup op, IEnumerable<string> detailsAsLines) GetDetailsAsTuple((OperationGroup, SpecBase<TModel, TMetadata>) tuple)
        {
            var (op, spec) = tuple;
            var detailsAsLines = spec switch
            {
                IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec
                    when binaryOperationSpec.Operation == operation 
                         && binaryOperationSpec.IsCollapsable =>
                    spec.ToEnumerable().GetBinaryDetailsAsLines(operation, level + 1),
                _ =>
                    spec.Description.GetDetailsAsLines()
            };

            return (op, detailsAsLines);
        }
    }
    
    internal static IEnumerable<(OperationGroup, SpecBase<TModel, TMetadata>)> IdentifyCollapsible<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specs,
        string operation)
    {
        return specs.SelectMany(spec =>
            spec switch
            {
                IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec
                    when binaryOperationSpec.Operation == operation
                         && binaryOperationSpec.IsCollapsable=>
                    IdentifyCollapsible(binaryOperationSpec, operation),
                _ =>
                    (OtherSpecs: OperationGroup.Other, spec).ToEnumerable()
            });
    }

    internal static IEnumerable<(OperationGroup, SpecBase<TModel, TMetadata>)> IdentifyCollapsible<TModel, TMetadata>(
        this IBinaryOperationSpec<TModel, TMetadata> spec,
        string operation)
    {
        var underlyingSpecs = spec.Left.ToEnumerable().Append(spec.Right);
    
        return underlyingSpecs
            .SelectMany(underlyingSpec => 
                underlyingSpec switch
                {
                    IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec
                        when binaryOperationSpec.Operation == operation
                             && binaryOperationSpec.IsCollapsable =>
                        binaryOperationSpec.GetUnderlyingSpecs().IdentifyCollapsible(operation),
                    _ => (OtherSpecs: OperationGroup.Other, underlyingSpec).ToEnumerable()
                });
    }
    
    public static IEnumerable<IEnumerable<T>> GroupAdjacentBy<T>(
        this IEnumerable<T> source, Func<T, T, bool> predicate)
    {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
            yield break;

        List<T> currentGroup = [enumerator.Current];
        var prev = enumerator.Current;

        while (enumerator.MoveNext())
        {
            if (predicate(prev, enumerator.Current))
            {
                currentGroup.Add(enumerator.Current);
            }
            else
            {
                yield return currentGroup;
                currentGroup = [enumerator.Current];
            }
            prev = enumerator.Current;
        }

        yield return currentGroup;
    }
    
    private static IEnumerable<SpecBase<TModel, TMetadata>> GetUnderlyingSpecs<TModel, TMetadata>(
        this IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec) =>
        binaryOperationSpec.Left.ToEnumerable().Append(binaryOperationSpec.Right);
}