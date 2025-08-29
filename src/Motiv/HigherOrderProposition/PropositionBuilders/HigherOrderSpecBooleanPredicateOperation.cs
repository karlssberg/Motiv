namespace Motiv.HigherOrderProposition.PropositionBuilders;

public readonly struct HigherOrderSpecBooleanPredicateOperation<TModel>(
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    internal Func<IEnumerable<ModelResult<TModel>>, bool> HigherOrderPredicate { get; } =
        higherOrderPredicate;

    internal Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> CauseSelector { get; } =
        causeSelector;
}
