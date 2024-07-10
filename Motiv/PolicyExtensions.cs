namespace Motiv;

/// <summary>Extension methods for <see cref="Policy{TInput, TOutput}" />.</summary>
public static class PolicyExtensions
{
    /// <summary>
    /// Combines a collection of <see cref="PolicyBase{TModel,TMetadata}" /> whereby the enumeration of the policies
    /// halts at the first policy that is satisfied.  If no policies are satisfied, the last policy's false metadata is
    /// returned.  This is equivalent to combining the policies using the <see cref="PolicyBase{TModel,TMetadata}.OrElse(Motiv.PolicyBase{TModel,TMetadata})" />
    /// method.
    /// </summary>
    /// <param name="propositions">The propositions to apply the ELSE operator to.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single specification that represents the conditional OR of all the input propositions.</returns>
    public static PolicyBase<TModel, TMetadata> OrElseTogether<TModel, TMetadata>(
        this IEnumerable<PolicyBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec.OrElse(rightSpec));
}
