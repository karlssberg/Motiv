using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv.HigherOrder;

internal sealed class HigherOrderSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue, 
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    string propositionalAssertion,
    AssertionSource assertionSource,
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
        var causes = GetCauses(isSatisfied, underlyingResults).ToArray();
        var booleanCollectionResults = new HigherOrderEvaluation<TModel, TUnderlyingMetadata>(
            underlyingResults, 
            causes);

        var metadata = isSatisfied
            ? whenTrue(booleanCollectionResults)
            : whenFalse(booleanCollectionResults);
        var metadataSet = new MetadataTree<TMetadata>(metadata, GetUnderlyingMetadataSets(underlyingResults));
        
        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            underlyingResults,
            metadataSet,
            causes,
            Proposition,
            assertionSource);
    }

    private IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> GetCauses(
        bool isSatisfied,
        ICollection<BooleanResult<TModel,TUnderlyingMetadata>> operandResults)
    {
        if (causeSelector is not null)
            return causeSelector(isSatisfied, operandResults);
        
        var trueOperands = GetTrueOperands(operandResults);
        var falseOperands = GetFalseOperands(operandResults);

        return isSatisfied switch
        {
            true when higherOrderPredicate(trueOperands) => trueOperands,
            true when higherOrderPredicate(falseOperands) => falseOperands,
            false when !higherOrderPredicate(trueOperands) && trueOperands.Count > 0 => trueOperands,
            false when !higherOrderPredicate(falseOperands) && falseOperands.Count > 0 => falseOperands,
            _ => operandResults
        };
    }
    
    private static ICollection<BooleanResult<TModel,TUnderlyingMetadata>> GetFalseOperands(
        IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> operandResults) =>
        operandResults.WhereFalse().ToArray();

    private static ICollection<BooleanResult<TModel,TUnderlyingMetadata>> GetTrueOperands(
        IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> operandResults) =>
        operandResults.WhereTrue().ToArray();

    private static IEnumerable<MetadataTree<TMetadata>> GetUnderlyingMetadataSets(
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> underlyingResults) =>
        underlyingResults switch
        {
            IEnumerable<BooleanResult<TModel, TMetadata>> results => results.Select(result => result.MetadataTree),
            _ => Enumerable.Empty<MetadataTree<TMetadata>>()
        };
}