namespace Motiv.HigherOrderProposition;

internal static class Causes
{
    internal static IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> Get<TModel, TUnderlyingMetadata>(
        bool isSatisfied,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>> operandResults,
        Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        Resolve(isSatisfied, operandResults, higherOrderPredicate, result => result.Satisfied);

    internal static IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> Get<TModel, TUnderlyingMetadata>(
        bool isSatisfied,
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>> operandResults,
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        Resolve(isSatisfied, operandResults, higherOrderPredicate, result => result.Satisfied);

    internal static IEnumerable<ModelResult<TModel>> Get<TModel>(
        bool isSatisfied,
        IEnumerable<ModelResult<TModel>> operandResults,
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate) =>
        Resolve(isSatisfied, operandResults, higherOrderPredicate, result => result.Satisfied);

    private static IEnumerable<T> Resolve<T>(
        bool isSatisfied,
        IEnumerable<T> operandResults,
        Func<IEnumerable<T>, bool> higherOrderPredicate,
        Func<T, bool> satisfiedSelector)
    {
        var operandResultArray = operandResults.ToArray();
        var trueAndFalseOperands = operandResultArray
            .GroupBy(satisfiedSelector)
            .Select(grouping => grouping.ToArray())
            .ToArray();

        var trueOperands = trueAndFalseOperands.ElementAtOrDefault(0) ?? [];
        var falseOperands = trueAndFalseOperands.ElementAtOrDefault(1) ?? [];

        var candidateCauses = isSatisfied switch
        {
            true when higherOrderPredicate(trueOperands) => trueOperands,
            true when higherOrderPredicate(falseOperands) => falseOperands,
            false when !higherOrderPredicate(trueOperands) && trueOperands.Length > 0 => trueOperands,
            false when !higherOrderPredicate(falseOperands) && falseOperands.Length > 0 => falseOperands,
            _ => operandResultArray
        };

        return candidateCauses
            .ElseIfEmpty(operandResultArray);
    }
}
