namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateExplanationProposition<TModel>(
    Func<TModel,bool> predicate, 
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> falseBecause,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override ISpecDescription Description => specDescription;

    public override BooleanResultBase<string> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new ModelResult<TModel>(model, predicate(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        
        var assertion = new Lazy<string>(() =>
        {
            var causes = causeSelector(isSatisfied, underlyingResults).ToArray();
            
            var evaluation = new HigherOrderBooleanEvaluation<TModel>(underlyingResults, causes);
            
            return isSatisfied
                ? trueBecause(evaluation)
                : falseBecause(evaluation);
        });

        var metadataTree = new Lazy<MetadataNode<string>>(() => 
            new MetadataNode<string>(assertion.Value));

        var explanation = new Lazy<Explanation>(() =>
            new Explanation(assertion.Value, []));

        return new HigherOrderFromBooleanPredicateBooleanResult<string>(
            isSatisfied,
            Metadata,
            Explanation,
            Reason);
        
        MetadataNode<string> Metadata() => metadataTree.Value;
        Explanation Explanation() => explanation.Value;
        string Reason() => assertion.Value;
    }
}