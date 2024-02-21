namespace Karlssberg.Motiv.Propositions;

internal sealed class HigherOrderSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec, 
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue, 
    Func<BooleanCollectionResult<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenFalse,
    string description)
    : SpecBase<IEnumerable<TModel>, TMetadata>
{
    public override string Description => $"<{description}>({underlyingSpec.Description})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var underlyingResults = models
            .Select(model => (model, result: underlyingSpec.IsSatisfiedByOrWrapException(model)))
            .Select(tuple => new BooleanResult<TModel, TUnderlyingMetadata>(tuple.model, tuple.result))
            .ToArray();
        
        var isSatisfied = higherOrderPredicate(underlyingResults);
        var causes = GetCauses(isSatisfied, underlyingResults).ToArray();
        var booleanCollectionResults = new BooleanCollectionResult<TModel, TUnderlyingMetadata>(
            underlyingResults, 
            causes);

        var metadata = isSatisfied
            ? whenTrue(booleanCollectionResults)
            : whenFalse(booleanCollectionResults);

        return new HigherOrderBooleanResult<TModel, TMetadata, TUnderlyingMetadata>(
            isSatisfied,
            metadata.Distinct(),
            underlyingResults,
            causes,
            description);
    }
    
    private IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> GetCauses(
        bool isSatisfied,
        ICollection<BooleanResult<TModel,TUnderlyingMetadata>> operandResults)
    {
        var trueOperands = GetTrueOperands(operandResults);
        var falseOperands = GetFalseOperands(operandResults);

        return isSatisfied switch
        {
            true when higherOrderPredicate(trueOperands) => trueOperands,
            true when higherOrderPredicate(falseOperands) => falseOperands,
            false when !higherOrderPredicate(trueOperands) && trueOperands.Any() => trueOperands,
            false when !higherOrderPredicate(falseOperands) && falseOperands.Any() => falseOperands,
            _ => operandResults
        };
    }
    
    

    private static ICollection<BooleanResult<TModel,TUnderlyingMetadata>> GetFalseOperands(
        IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> operandResults) =>
        operandResults
            .Where(result => !result.Satisfied)
            .ToArray();

    private static ICollection<BooleanResult<TModel,TUnderlyingMetadata>> GetTrueOperands(
        IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> operandResults) =>
        operandResults
            .Where(result => result.Satisfied)
            .ToArray();
    
}