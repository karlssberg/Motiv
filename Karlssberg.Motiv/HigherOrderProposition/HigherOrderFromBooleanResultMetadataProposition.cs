namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override ISpecDescription Description => specDescription;

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

        var assertion = metadata switch
        {
            string because => because,
            _ => specDescription.ToReason(isSatisfied),
        };
        
        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadata.ToEnumerable(),
            assertion.ToEnumerable(),
            Description.ToReason(isSatisfied),
            underlyingResults,
            causes);
    }
}