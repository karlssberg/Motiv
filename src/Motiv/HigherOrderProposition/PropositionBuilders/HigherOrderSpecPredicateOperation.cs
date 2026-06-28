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
public readonly struct HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>(
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    internal Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> HigherOrderPredicate { get; } =
        higherOrderPredicate;

    internal Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> CauseSelector { get; } =
        causeSelector;
}
