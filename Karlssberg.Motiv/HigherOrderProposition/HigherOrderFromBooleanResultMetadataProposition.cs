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
        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]> (() => Causes
            .Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector)
            .ToArray());
        
        var metadata = new Lazy<TMetadata>(() =>
        {
            var booleanCollectionResults = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
                underlyingResults, 
                causes.Value);
            
            return isSatisfied
                ? whenTrue(booleanCollectionResults)
                : whenFalse(booleanCollectionResults);
        });

        var assertion = new Lazy<IEnumerable<string>>(() => 
            metadata.Value switch
            {
                string because => because.ToEnumerable(),
                _ => specDescription.ToReason(isSatisfied).ToEnumerable(),
            });

        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            Metadata,
            Assertions,
            Reason,
            underlyingResults,
            GetCauses);
        
        IEnumerable<TMetadata> Metadata() => metadata.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertion.Value;
        string Reason() => Description.ToReason(isSatisfied);
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}