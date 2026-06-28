namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// Represents a higher-order policy predicate operation.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
/// <param name="higherOrderPredicate">
/// A function that takes an enumerable of policy results and returns a boolean indicating
/// whether the higher-order predicate is satisfied.
/// </param>
/// <param name="causeSelector">
/// A function that takes a boolean and an enumerable of policy results, and returns an enumerable
/// of policy results based on the cause selection logic.
/// </param>
public readonly struct HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>(
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    internal Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> HigherOrderPredicate { get; } =
        higherOrderPredicate;

    internal Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> CauseSelector { get; } =
        causeSelector;
}
