namespace Motiv;

/// <summary>
/// Provides extension methods for working with enumerable collections.
/// </summary>
public static class BooleanResultExtensions
{
    /// <summary>
    /// Combines a collection of boolean results using the logical AND operator (i.e. `&amp;`).
    /// </summary>
    /// <param name="booleanResults">The boolean results to apply the AND operator to.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single boolean result that is the logical AND of all the input boolean results.</returns>
    public static BooleanResultBase<TMetadata> AndTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResults) =>
        booleanResults.Aggregate((left, right) => left & right);

    /// <summary>
    /// Combines a collection of propositions using the conditional AND operator (i.e. `&amp;&amp;`).
    /// </summary>
    /// <param name="booleanResults">The boolean results to apply the AND operator to.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single boolean result that is the conditional AND of all the input boolean results.</returns>
    public static BooleanResultBase<TMetadata> AndAlsoTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResults) =>
        booleanResults.Aggregate((left, right) => left.AndAlso(right));

    /// <summary>
    /// Combines a collection of boolean results using the logical OR operator (i.e. `|`).
    /// </summary>
    /// <param name="booleanResult">The boolean results to apply the OR operator to.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single boolean result that is the logical OR of all the input propositions.</returns>
    public static BooleanResultBase<TMetadata> OrTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResult) =>
        booleanResult.Aggregate((left, right) => left | right);

    /// <summary>
    /// Combines a collection of boolean results using the conditional OR operator (i.e. `||`).
    /// </summary>
    /// <param name="booleanResult">The boolean results to apply the OR operator to.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single boolean result that is the conditional OR of all the input propositions.</returns>
    public static BooleanResultBase<TMetadata> OrElseTogether<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> booleanResult) =>
        booleanResult.Aggregate((left, right) => left.OrElse(right));

    /// <summary>
    /// Filters a collection of boolean results, returning only those where the result is true.
    /// </summary>
    /// <param name="results">The collection of BooleanResultBase to filter.</param>
    /// <typeparam name="TBooleanResult">The specific type of BooleanResultBase in the collection.</typeparam>
    /// <returns>A collection of boolean results where the result is true.</returns>
    public static IEnumerable<TBooleanResult> WhereTrue<TBooleanResult>(
        this IEnumerable<TBooleanResult> results)
        where TBooleanResult : BooleanResultBase =>
        results.Where(result => result.Satisfied);

    /// <summary>
    /// Filters a collection of boolean results, returning only those where the result is false.
    /// </summary>
    /// <param name="results">The collection of BooleanResultBase to filter.</param>
    /// <typeparam name="TBooleanResult">The specific type of BooleanResultBase in the collection.</typeparam>
    /// <returns>A collection of boolean results where the result is false.</returns>
    public static IEnumerable<TBooleanResult> WhereFalse<TBooleanResult>(
        this IEnumerable<TBooleanResult> results)
        where TBooleanResult : BooleanResultBase =>
        results.Where(result => !result.Satisfied);

    /// <summary>
    /// Counts the number of boolean results in a collection where the result is true.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to count.</param>
    /// <typeparam name="TMetadata">The specific type of metadata used by <see cref="BooleanResultBase{TMetadata}"/>.</typeparam>
    /// <returns>The count of boolean results where the result is true.</returns>
    public static int CountTrue<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Count(result => result.Satisfied);

    /// <summary>
    /// Counts the number of boolean results in a collection where the result is false.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to count.</param>
    /// <typeparam name="TMetadata">The specific type of metadata used by <see cref="BooleanResultBase{TMetadata}"/>.</typeparam>
    /// <returns>The count of boolean results where the result is false.</returns>
    public static int CountFalse<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Count(result => !result.Satisfied);

    /// <summary>
    /// Checks if all boolean results in a collection are true.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to check.</param>
    /// <typeparam name="TMetadata">The specific type of metadata in the <see cref="BooleanResultBase{TMetadata}"/>.</typeparam>
    /// <returns>True if all boolean results are true, false otherwise.</returns>
    public static bool AllTrue<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.All(result => result.Satisfied);

    /// <summary>
    /// Checks if all boolean results in a collection are false.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to check.</param>
    /// <typeparam name="TMetadata">The specific type of metadata in the <see cref="BooleanResultBase{TMetadata}"/>.</typeparam>
    /// <returns>True if all boolean results are false, false otherwise.</returns>
    public static bool AllFalse<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.All(result => !result.Satisfied);

    /// <summary>
    /// Checks if any boolean result in a collection are true.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to check.</param>
    /// <typeparam name="TMetadata">The specific type of metadata in the <see cref="BooleanResultBase{TMetadata}"/>.</typeparam>
    /// <returns>True if any boolean result is true, false otherwise.</returns>
    public static bool AnyTrue<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Any(result => result.Satisfied);

    /// <summary>
    /// Checks if any boolean result in a collection are false.
    /// </summary>
    /// <param name="results">The collection of <see cref="BooleanResultBase{TMetadata}"/> to check.</param>
    /// <typeparam name="TMetadata">The specific type of metadata in the <see cref="BooleanResultBase{TMetadata}"/>.</typeparam>
    /// <returns>True if any boolean result is false, false otherwise.</returns>
    public static bool AnyFalse<TMetadata>(
        this IEnumerable<BooleanResultBase<TMetadata>> results) =>
        results.Any(result => !result.Satisfied);

    internal static IEnumerable<string> GetBinaryJustificationAsLines(
        this IEnumerable<BooleanResultBase> causalResults,
        string conjunction,
        int level = 0)
    {
        var adjacentLineGroups = causalResults
            .IdentifyCollapsible(conjunction)
            .Select(GetJustificationAsTuple)
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
                yield return conjunction;

            foreach (var line in group.detailsAsLines)
                yield return line.Indent();
        }

        yield break;

        (OperationGroup op, IEnumerable<string> detailsAsLines) GetJustificationAsTuple((OperationGroup, BooleanResultBase) tuple)
        {
            var (op, result) = tuple;
            var detailsAsLines = result switch
            {
                IBinaryBooleanOperationResult binaryOperationResult
                    when binaryOperationResult.Operation == conjunction
                         && binaryOperationResult.IsCollapsable =>
                    result.ToEnumerable().GetBinaryJustificationAsLines(conjunction, level + 1),
                _ =>
                    result.Description.GetJustificationAsLines()
            };

            return (op, detailsAsLines);
        }
    }

    private static IEnumerable<BooleanResultBase> AggregateUnderlyingCauses(
        this BooleanResultBase result,
        int atDepth = 0) =>
        result switch
        {
            IBinaryBooleanOperationResult => result.Causes.AggregateUnderlyingCauses(atDepth),
            _ when atDepth > 0 => result.Causes.AggregateUnderlyingCauses(atDepth - 1),
            _ => result.ToEnumerable()
        };

    private static IEnumerable<BooleanResultBase> AggregateUnderlyingCauses(
        this IEnumerable<BooleanResultBase> underlyingResult,
        int atDepth = 0) =>
        underlyingResult.SelectMany(result => AggregateUnderlyingCauses(result,atDepth));

    private static IEnumerable<(OperationGroup, BooleanResultBase)> IdentifyCollapsible(
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

    private static IEnumerable<(OperationGroup, BooleanResultBase)> IdentifyCollapsible(
        this IBooleanOperationResult binaryResult,
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
}
