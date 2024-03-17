namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderExplanationSpec<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> whenTrue, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> whenFalse,
    string propositionalAssertion,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IProposition Proposition =>
        new HigherOrderProposition<TModel, TUnderlyingMetadata>(propositionalAssertion, underlyingSpec);

    public override BooleanResultBase<string> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => (model, result: underlyingSpec.IsSatisfiedByOrWrapException(model)))
            .Select(tuple => new BooleanResult<TModel, TUnderlyingMetadata>(tuple.model, tuple.result))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = Causes.Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector).ToArray();
        var booleanCollectionResults = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
            underlyingResults, 
            causes);

        var because = isSatisfied
            ? whenTrue(booleanCollectionResults)
            : whenFalse(booleanCollectionResults);
        
        return new HigherOrderExplanationBooleanResult<TModel, TUnderlyingMetadata>(
            isSatisfied,
            underlyingResults,
            causes,
            because);
    }
}