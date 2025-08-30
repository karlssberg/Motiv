namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating higher-order propositions using a predicate function that evaluates a collection of model results.
/// </summary>
/// <param name="higherOrderPredicate">The higher-order predicate that evaluates a collection of model results to a boolean value.</param>
/// <param name="causeSelector">A function that selects causes based on the boolean result and the collection of model results.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly struct HigherOrderSpecBooleanPredicateOperation<TModel>(
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    internal Func<IEnumerable<ModelResult<TModel>>, bool> HigherOrderPredicate { get; } =
        higherOrderPredicate;

    internal Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> CauseSelector { get; } =
        causeSelector;
}
