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
public readonly struct HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>
{
    /// <summary>Initializes a new operation with a higher-order predicate and cause selector.</summary>
    public HigherOrderPolicyPredicateOperation(
        Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
        : this(higherOrderPredicate, causeSelector, null)
    {
    }

    internal HigherOrderPolicyPredicateOperation(
        Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector,
        HigherOrderShortCircuit? shortCircuit)
    {
        HigherOrderPredicate = higherOrderPredicate;
        CauseSelector = causeSelector;
        ShortCircuit = shortCircuit;
    }

    internal Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> HigherOrderPredicate { get; }

    internal Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> CauseSelector { get; }

    internal HigherOrderShortCircuit? ShortCircuit { get; }
}
