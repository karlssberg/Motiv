using Motiv.Shared;
using Motiv.SyncToAsyncAdapter;
using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Justification traversal for asynchronous compositions. Serves the same purpose as
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
    /// Builds the justification lines for a mixed binary composition, flattening operands that belong to the
    /// same collapsible operation (whether async operator specs, adapter-lifted sync specs, or native sync
    /// operator specs) into a single group beneath one operation heading.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="specs">The operands to render. Assumed to be non-empty, as every binary composition has two operands.</param>
    /// <param name="operation">The name of the operation being rendered (e.g. <see cref="Operator.And" />).</param>
    /// <returns>The justification lines for the composition.</returns>
    internal static IEnumerable<string> GetMixedBinaryJustificationAsLines<TModel, TMetadata>(
        this IEnumerable<SpecBase> specs,
        string operation) =>
        operation
            .ToEnumerable()
            .Concat(specs
                .FlattenCollapsible<TModel, TMetadata>(operation)
                .SelectMany(spec => spec.Description.GetDetailsAsLines())
                .Select(line => line.Indent()));

    private static IEnumerable<SpecBase> FlattenCollapsible<TModel, TMetadata>(
        this IEnumerable<SpecBase> specs,
        string operation) =>
        specs.SelectMany(spec =>
            spec.Unwrap() switch
            {
                IAsyncBinaryOperationSpec<TModel, TMetadata> asyncBinary when asyncBinary.CollapsesInto(operation) =>
                    Operands(asyncBinary.Left, asyncBinary.Right).FlattenCollapsible<TModel, TMetadata>(operation),
                IBinaryOperationSpec<TModel, TMetadata> syncBinary when syncBinary.CollapsesInto(operation) =>
                    Operands(syncBinary.Left, syncBinary.Right).FlattenCollapsible<TModel, TMetadata>(operation),
                _ => spec.ToEnumerable()
            });

    private static bool CollapsesInto(this IBooleanOperationSpec spec, string operation) =>
        spec.Operation == operation && spec.IsCollapsable;

    private static IEnumerable<SpecBase> Operands(SpecBase left, SpecBase right) =>
        left.ToEnumerable().Append(right);
}
