namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateMetadataProposition<TModel, TMetadata>(
    Func<TModel,bool> predicate, 
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override ISpecDescription Description => specDescription;

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
            string because => because.ToEnumerable(),                    
            IEnumerable<string> because => because,
            _ => specDescription.ToReason(isSatisfied).ToEnumerable()
        };

        return new HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
            isSatisfied,
            new MetadataTree<TMetadata>(metadata),
            new Explanation(assertion),
            specDescription.ToReason(isSatisfied));
    }
}