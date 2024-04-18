namespace Karlssberg.Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanResultExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override ISpecDescription Description => specDescription;

    public override BooleanResultBase<string> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model)))
            .ToArray();
        var isSatisfied = higherOrderPredicate(underlyingResults);
        
        var causes = new Lazy<BooleanResult<TModel, TUnderlyingMetadata>[]>(() => 
            causeSelector(isSatisfied, underlyingResults)
                .ElseIfEmpty(underlyingResults)
                .ToArray());
        
        var assertion = new Lazy<string>(() =>
        {
            var booleanCollectionResults = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
                    underlyingResults, 
                    causes.Value);
            
            return isSatisfied
                ? trueBecause(booleanCollectionResults)
                : falseBecause(booleanCollectionResults);
        });

        return new HigherOrderBooleanResult<TModel, string, TUnderlyingMetadata>(
            isSatisfied,
            Metadata,
            Assertions,
            Reason,
            underlyingResults,
            GetCauses);

        IEnumerable<string> Metadata() => assertion.Value.ToEnumerable();
        IEnumerable<string> Assertions() => assertion.Value.ToEnumerable();
        string Reason() => assertion.Value;
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> GetCauses() => causes.Value;
    }
}