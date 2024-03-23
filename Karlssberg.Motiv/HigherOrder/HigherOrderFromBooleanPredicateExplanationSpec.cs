namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderFromBooleanPredicateExplanationSpec<TModel>(
    Func<TModel,bool> predicate, 
    Func<IEnumerable<(TModel, bool)>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> falseBecause,
    IProposition proposition,
    Func<bool, IEnumerable<(TModel, bool)>, IEnumerable<(TModel, bool)>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IProposition Proposition => proposition;

    public override BooleanResultBase<string> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => (model, predicate(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = Causes.Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector).ToArray();
        var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

        var metadata = isSatisfied
            ? trueBecause(evaluation)
            : falseBecause(evaluation);

        return new HigherOrderFromBooleanPredicateBooleanResult<string>(
            isSatisfied,
            metadata,
            metadata);
    }
}