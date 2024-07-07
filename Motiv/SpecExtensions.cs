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

    internal static Func<TModel, BooleanResultBase<TMetadata>> ToBooleanResultPredicate<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec) =>
        spec.IsSatisfiedBy;
    
    internal static IEnumerable<string> GetBinaryJustificationAsLines<TModel, TMetadata>(
        this IEnumerable<SpecBase<TModel, TMetadata>> specs,
        string operation,
        int level  = 0)
    {
        var adjacentLineGroups = specs
            .IdentifyCollapsible(operation)
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

        (OperationGroup op, IEnumerable<string> detailsAsLines) GetJustificationAsTuple((OperationGroup, SpecBase<TModel, TMetadata>) tuple)
        {
            var (op, spec) = tuple;
            var detailsAsLines = spec switch
            {
                IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec
                    when binaryOperationSpec.Operation == operation 
                         && binaryOperationSpec.IsCollapsable =>
                    spec.ToEnumerable().GetBinaryJustificationAsLines(operation, level + 1),
                _ =>
                    spec.Description.GetDetailsAsLines()
            };

            return (op, detailsAsLines);
        }
    }
    
    private static IEnumerable<(OperationGroup, SpecBase<TModel, TMetadata>)> IdentifyCollapsible<TModel, TMetadata>(
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

    private static IEnumerable<(OperationGroup, SpecBase<TModel, TMetadata>)> IdentifyCollapsible<TModel, TMetadata>(
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

    private static IEnumerable<SpecBase<TModel, TMetadata>> GetUnderlyingSpecs<TModel, TMetadata>(
        this IBinaryOperationSpec<TModel, TMetadata> binaryOperationSpec) =>
        binaryOperationSpec.Left.ToEnumerable().Append(binaryOperationSpec.Right);
}