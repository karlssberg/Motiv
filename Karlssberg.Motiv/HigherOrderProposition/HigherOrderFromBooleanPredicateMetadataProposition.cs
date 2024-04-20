namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateMetadataProposition<TModel, TMetadata>(
    Func<TModel,bool> predicate, 
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue, 
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override ISpecDescription Description => specDescription;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var metadata = new Lazy<TMetadata>(() =>
        {
            var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
            var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);

            return isSatisfied
                ? whenTrue(evaluation)
                : whenFalse(evaluation);
        });
        
        var assertion = new Lazy<IEnumerable<string>>(() => 
            metadata.Value switch
            {
                string because => because.ToEnumerable(),                    
                IEnumerable<string> because => because,
                _ => specDescription.ToReason(isSatisfied).ToEnumerable()
            });

        return new HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
            isSatisfied,
            Metadata,
            Explanation,
            Reason);

        MetadataNode<TMetadata> Metadata() => new(metadata.Value);
        Explanation Explanation() => new(assertion.Value, []);
        string Reason() => specDescription.ToReason(isSatisfied);
    }
}