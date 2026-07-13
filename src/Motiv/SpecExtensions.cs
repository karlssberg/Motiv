using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv;

/// <summary>
/// Provides extension methods for predicates. These methods convert predicates into propositions.
/// </summary>
public static class SpecExtensions
{
    /// <summary>
    /// Combines a collection of propositions using the logical AND operator (i.e. `&amp;`)..
    /// </summary>
    /// <param name="propositions">the propositions to apply the AND operator to.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single specification that represents the logical AND of all the input propositions.</returns>
    public static SpecBase<TModel, TMetadata> AndTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec & rightSpec);

    /// <summary>
    /// Combines a collection of propositions using the conditional AND operator (i.e. `&amp;&amp;`).
    /// </summary>
    /// <param name="propositions">the propositions to apply the AND operator to.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single specification that represents the conitional AND of all the input boolean results.</returns>
    public static SpecBase<TModel, TMetadata> AndAlsoTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec.AndAlso(rightSpec));

    /// <summary>
    /// Combines a collection of propositions using the logical OR operator (i.e. `|`).
    /// </summary>
    /// <param name="propositions">The propositions to apply the OR operator to</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single specification that represents the logical OR of all the input propositions.</returns>
    public static SpecBase<TModel, TMetadata> OrTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec | rightSpec);

    /// <summary>
    /// Combines a collection of propositions using the conditional OR operator (i.e. `||`).
    /// </summary>
    /// <param name="propositions">The propositions to apply the OR operator to.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single specification that represents the conditional OR of all the input propositions.</returns>
    public static SpecBase<TModel, TMetadata> OrElseTogether<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec.OrElse(rightSpec));


    /// <summary>
    /// Builds the justification lines for a binary composition, flattening operands that belong to the same
    /// collapsible operation into a single group beneath one operation heading.
    /// </summary>
    /// <param name="specs">The operands to render. Assumed to be non-empty, as every binary composition has two operands.</param>
    /// <param name="operation">The name of the operation being rendered (e.g. <see cref="Operator.And" />).</param>
    internal static IEnumerable<string> GetBinaryJustificationAsLines<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specs,
        string operation) =>
        operation
            .ToEnumerable()
            .Concat(specs
                .FlattenCollapsible(operation)
                .SelectMany(spec => spec.Description.GetDetailsAsLines())
                .Select(line => line.Indent()));

    private static IEnumerable<SpecBase<TModel, TMetadata>> FlattenCollapsible<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specs,
        string operation) =>
        specs.SelectMany(spec =>
            spec switch
            {
                IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec
                    when binaryOperationSpec.Operation == operation
                         && binaryOperationSpec.IsCollapsable =>
                    binaryOperationSpec.GetUnderlyingSpecs().FlattenCollapsible(operation),
                _ => spec.ToEnumerable()
            });

    private static IEnumerable<SpecBase<TModel, TMetadata>> GetUnderlyingSpecs<TModel, TMetadata>(
        this IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec) =>
        binaryOperationSpec.Left.ToEnumerable().Append(binaryOperationSpec.Right);
}
