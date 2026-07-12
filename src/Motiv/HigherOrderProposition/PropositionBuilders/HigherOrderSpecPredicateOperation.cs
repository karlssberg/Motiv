namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// Represents a higher-order specification predicate operation.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
/// <param name="higherOrderPredicate">
/// A function that takes an enumerable of boolean results and returns a boolean indicating
/// whether the higher-order predicate is satisfied.
/// </param>
/// <param name="causeSelector">
/// A function that takes a boolean and an enumerable of boolean results, and returns an enumerable
/// of boolean results based on the cause selection logic.
/// </param>
public readonly struct HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>
{
    /// <summary>Initializes a new operation with a higher-order predicate and cause selector.</summary>
    public HigherOrderSpecPredicateOperation(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
        : this(higherOrderPredicate, causeSelector, null)
    {
    }

    internal HigherOrderSpecPredicateOperation(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector,
        HigherOrderShortCircuit? shortCircuit)
    {
        HigherOrderPredicate = higherOrderPredicate;
        CauseSelector = causeSelector;
        ShortCircuit = shortCircuit;
    }

    internal Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> HigherOrderPredicate { get; }

    internal Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> CauseSelector { get; }

    internal HigherOrderShortCircuit? ShortCircuit { get; }
}
