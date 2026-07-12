namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating higher-order propositions using a predicate function that evaluates a collection of model results.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly struct HigherOrderSpecBooleanPredicateOperation<TModel>
{
    /// <summary>Initializes a new operation with a higher-order predicate and cause selector.</summary>
    public HigherOrderSpecBooleanPredicateOperation(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
        : this(higherOrderPredicate, causeSelector, null)
    {
    }

    internal HigherOrderSpecBooleanPredicateOperation(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector,
        HigherOrderShortCircuit? shortCircuit)
    {
        HigherOrderPredicate = higherOrderPredicate;
        CauseSelector = causeSelector;
        ShortCircuit = shortCircuit;
    }

    internal Func<IEnumerable<ModelResult<TModel>>, bool> HigherOrderPredicate { get; }

    internal Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> CauseSelector { get; }

    internal HigherOrderShortCircuit? ShortCircuit { get; }
}
