namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderFromBooleanPredicateMetadataSpec<TModel, TMetadata>(
    Func<TModel,bool> predicate, 
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    IProposition proposition,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IProposition Proposition => proposition;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = Causes.Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector).ToArray();
        var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

        var metadata = isSatisfied
            ? whenTrue(evaluation)
            : whenFalse(evaluation);
        
        var assertion = metadata switch
        {
            string because => because,
            _ => proposition.ToReason(isSatisfied),
        };

        return new HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
            isSatisfied,
            metadata,
            assertion,
            proposition.ToReason(isSatisfied));
    }
}