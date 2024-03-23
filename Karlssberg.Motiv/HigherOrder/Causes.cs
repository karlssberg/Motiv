namespace Karlssberg.Motiv.HigherOrder;

internal static class Causes
{
    internal static IEnumerable<BooleanResult<TModel,TUnderlyingMetadata>> Get<TModel, TUnderlyingMetadata>(
        bool isSatisfied,
        ICollection<BooleanResult<TModel,TUnderlyingMetadata>> operandResults,
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate, 
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector)
    {
        if (causeSelector is not null)
            return causeSelector(isSatisfied, operandResults);
            
        var trueOperands = operandResults.WhereTrue().ToArray();
        var falseOperands = operandResults.WhereFalse().ToArray();

        return isSatisfied switch
        {
            true when higherOrderPredicate(trueOperands) => trueOperands,
            true when higherOrderPredicate(falseOperands) => falseOperands,
            false when !higherOrderPredicate(trueOperands) && trueOperands.Length > 0 => trueOperands,
            false when !higherOrderPredicate(falseOperands) && falseOperands.Length > 0 => falseOperands,
            _ => operandResults
        };
    }
    
    internal static IEnumerable<(TModel, bool)> Get<TModel>(
        bool isSatisfied,
        ICollection<(TModel model, bool satisfied)> operandResults,
        Func<IEnumerable<(TModel, bool)>, bool> higherOrderPredicate, 
        Func<bool, IEnumerable<(TModel, bool)>, IEnumerable<(TModel, bool)>>? causeSelector)
    {
        if (causeSelector is not null)
            return causeSelector(isSatisfied, operandResults);
            
        var trueOperands = operandResults.Where(m => m.satisfied).ToArray();
        var falseOperands = operandResults.Where(m => !m.satisfied).ToArray();

        return isSatisfied switch
        {
            true when higherOrderPredicate(trueOperands) => trueOperands,
            true when higherOrderPredicate(falseOperands) => falseOperands,
            false when !higherOrderPredicate(trueOperands) && trueOperands.Length > 0 => trueOperands,
            false when !higherOrderPredicate(falseOperands) && falseOperands.Length > 0 => falseOperands,
            _ => operandResults
        };
    }
    
}