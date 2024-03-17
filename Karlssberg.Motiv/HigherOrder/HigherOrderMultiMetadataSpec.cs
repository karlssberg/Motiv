namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderMultiMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    string propositionalAssertion,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override IProposition Proposition =>
        new HigherOrderProposition<TModel, TUnderlyingMetadata>(propositionalAssertion, underlyingSpec);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
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

        var metadata = isSatisfied
            ? whenTrue(booleanCollectionResults)
            : whenFalse(booleanCollectionResults);
        
        var metadataSet = new MetadataTree<TMetadata>(
            metadata,
            underlyingResults.ResolveMetadataSets<TMetadata, TUnderlyingMetadata>());
        
        return new HigherOrderMultiMetadataBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            underlyingResults,
            metadataSet,
            causes,
            Proposition);
    }

    
}