namespace Motiv;

/// <summary>
/// Extension methods for <see cref="PolicyResultBase{TMetadata}" />.
/// </summary>
public static class PolicyResultExtensions
{
    /// <summary>
    /// Combines a collection of policy results using the conditional OR operator (i.e. `||`).
    /// </summary>
    /// <param name="policyResult">The policy results to apply the OR operator to.</param>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single policy result that is the conditional OR of all the input propositions.</returns>
    public static PolicyResultBase<TMetadata> OrElseTogether<TMetadata>(
        this IEnumerable<PolicyResultBase<TMetadata>> policyResult) =>
        policyResult.Aggregate((left, right) => left.OrElse(right));
}
