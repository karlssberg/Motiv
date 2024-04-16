namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override ISpecDescription Description => specDescription;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model,  resultResolver(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = Causes.Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector).ToArray();
        var evaluation = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
            underlyingResults, 
            causes);

        var metadata = isSatisfied
            ? whenTrue(evaluation)
            : whenFalse(evaluation);
        
        var assertions = metadata switch {
            IEnumerable<string>  reasons => reasons,
            _ => specDescription.ToReason(isSatisfied).ToEnumerable()
        };

        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadata,
            assertions,
            specDescription.ToReason(isSatisfied),
            underlyingResults,
            causes);
    }
}