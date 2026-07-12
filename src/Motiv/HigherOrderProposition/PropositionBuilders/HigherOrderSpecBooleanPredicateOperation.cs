namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating higher-order propositions using a predicate function that evaluates a collection of model results.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly struct HigherOrderSpecBooleanPredicateOperation<TModel>(
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector,
    HigherOrderShortCircuit? shortCircuit = null)

{
    internal Func<IEnumerable<ModelResult<TModel>>, bool> HigherOrderPredicate { get; } = higherOrderPredicate;

    internal Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> CauseSelector { get; } = causeSelector;

    internal HigherOrderShortCircuit? ShortCircuit { get; } = shortCircuit;
}
