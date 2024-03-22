namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse,
    IProposition proposition,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IProposition Proposition => proposition;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = Causes.Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector).ToArray();
        var booleanCollectionResults = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
            underlyingResults, 
            causes);

        var metadata = isSatisfied
            ? whenTrue(booleanCollectionResults)
            : whenFalse(booleanCollectionResults);
        
        return new HigherOrderMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            underlyingResults,
            metadata,
            causes,
            Proposition);
    }
}