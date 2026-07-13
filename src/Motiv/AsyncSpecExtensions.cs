using Motiv.Shared;
using Motiv.SyncToAsyncAdapter;
using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Justification traversal for asynchronous compositions. Mirrors
/// <see cref="SpecExtensions.GetBinaryJustificationAsLines{TModel, TMetadata}" /> but operates over the
/// non-generic <see cref="SpecBase" /> so that async operator specs and adapter-lifted synchronous specs
/// collapse identically to an all-synchronous composition.
/// </summary>
internal static class AsyncSpecExtensions
{
    /// <summary>
    /// Unwraps a synchronous-to-asynchronous adapter to its underlying synchronous specification so that
    /// lifted sync operands classify and collapse identically to native sync composition.
    /// </summary>
    /// <param name="spec">The specification to unwrap.</param>
    /// <returns>The underlying synchronous specification if <paramref name="spec" /> is an adapter; otherwise <paramref name="spec" />.</returns>
    internal static SpecBase Unwrap(this SpecBase spec) =>
        spec is ISyncSpecAdapter adapter ? adapter.UnderlyingSpec : spec;

    /// <summary>
    /// Builds the justification lines for a mixed binary composition, collapsing adjacent operands that
    /// belong to the same collapsible operation (whether async operator specs, adapter-lifted sync specs,
    /// or native sync operator specs) into a single flattened group.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specs">The operands to render.</param>
    /// <param name="operation">The name of the operation being rendered (e.g. <see cref="Operator.And" />).</param>
    /// <param name="level">The current recursion depth, used to suppress the operation heading at the root.</param>
    /// <returns>The justification lines for the composition.</returns>
    internal static IEnumerable<string> GetMixedBinaryJustificationAsLines<TModel, TMetadata>(
        this IEnumerable<SpecBase> specs,
        string operation,
        int level = 0)
    {
        var adjacentLineGroups = specs
            .IdentifyCollapsible<TModel, TMetadata>(operation)
            .Select(GetJustificationAsTuple)
            .GroupAdjacentBy((prev, next) => prev.op == next.op)
            .SelectMany(group =>
            {
                var groupArray = group.ToArray();
                if (groupArray.Length == 0)
                    return [];

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
                yield return line.Indent();
        }

        yield break;

        (OperationGroup op, IEnumerable<string> detailsAsLines) GetJustificationAsTuple((OperationGroup, SpecBase) tuple)
        {
            var (op, spec) = tuple;
            var detailsAsLines = spec.Unwrap() switch
            {
                (IAsyncBinaryOperationSpec<TModel, TMetadata> or IBinaryOperationSpec<TModel, TMetadata>) and IBooleanOperationSpec binaryOp
                    when binaryOp.Operation == operation && binaryOp.IsCollapsable =>
                    spec.ToEnumerable().GetMixedBinaryJustificationAsLines<TModel, TMetadata>(operation, level + 1),
                _ =>
                    spec.Description.GetDetailsAsLines()
            };

            return (op, detailsAsLines);
        }
    }

    private static IEnumerable<(OperationGroup, SpecBase)> IdentifyCollapsible<TModel, TMetadata>(
        this IEnumerable<SpecBase> specs,
        string operation)
    {
        return specs.SelectMany(spec =>
            spec.Unwrap() switch
            {
                IAsyncBinaryOperationSpec<TModel, TMetadata> asyncBinary
                    when asyncBinary.Operation == operation
                         && asyncBinary.IsCollapsable =>
                    asyncBinary.GetUnderlyingSpecs().IdentifyCollapsible<TModel, TMetadata>(operation),
                IBinaryOperationSpec<TModel, TMetadata> syncBinary
                    when syncBinary.Operation == operation
                         && syncBinary.IsCollapsable =>
                    syncBinary.GetUnderlyingSpecs().IdentifyCollapsible<TModel, TMetadata>(operation),
                _ => (OtherSpecs: OperationGroup.Other, spec).ToEnumerable()
            });
    }

    private static IEnumerable<SpecBase> GetUnderlyingSpecs<TModel, TMetadata>(
        this IAsyncBinaryOperationSpec<TModel, TMetadata> spec) =>
        ((SpecBase)spec.Left).ToEnumerable().Append(spec.Right);

    private static IEnumerable<SpecBase> GetUnderlyingSpecs<TModel, TMetadata>(
        this IBinaryOperationSpec<TModel, TMetadata> spec) =>
        ((SpecBase)spec.Left).ToEnumerable().Append(spec.Right);
}
