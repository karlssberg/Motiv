﻿namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderExplanationSpec<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause,
    IProposition proposition,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    : SpecBase<IEnumerable<TModel>, string>
{
    public override IProposition Proposition => proposition;

    public override BooleanResultBase<string> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => new BooleanResult<TModel, TUnderlyingMetadata>(model, resultResolver(model)))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = Causes.Get(isSatisfied, underlyingResults, higherOrderPredicate, causeSelector).ToArray();
        var booleanCollectionResults = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
            underlyingResults, 
            causes);

        var assertion = isSatisfied
            ? trueBecause(booleanCollectionResults)
            : falseBecause(booleanCollectionResults);
        
        return new HigherOrderBooleanResult<TModel, string, TUnderlyingMetadata>(
            isSatisfied,
            assertion.ToEnumerable(),
            underlyingResults,
            causes,
            assertion.ToEnumerable(),
            assertion);
    }
}