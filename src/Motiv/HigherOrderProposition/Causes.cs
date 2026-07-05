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

    /// <summary>
    ///     Selects the satisfied results as the causes, falling back to all results when none are satisfied.
    ///     Used by the count-based higher-order propositions (at-least-n, at-most-n, exactly-n).
    /// </summary>
    internal static IEnumerable<TBooleanResult> SatisfiedElseAll<TBooleanResult>(
        IEnumerable<TBooleanResult> results)
        where TBooleanResult : BooleanResultBase
    {
        var resultsArray = results as TBooleanResult[] ?? results.ToArray();
        return resultsArray.WhereTrue().ElseIfEmpty(resultsArray);
    }

    /// <summary>
    ///     Selects the satisfied results as the causes, falling back to all results when none are satisfied.
    ///     Used by the count-based higher-order propositions (at-least-n, at-most-n, exactly-n).
    /// </summary>
    internal static IEnumerable<ModelResult<TModel>> SatisfiedElseAll<TModel>(
        IEnumerable<ModelResult<TModel>> results)
    {
        var resultsArray = results as ModelResult<TModel>[] ?? results.ToArray();
        return resultsArray.WhereTrue().ElseIfEmpty(resultsArray);
    }

    private static IEnumerable<T> Resolve<T>(
        bool isSatisfied,
        IEnumerable<T> operandResults,
        Func<IEnumerable<T>, bool> higherOrderPredicate,
        Func<T, bool> satisfiedSelector)
    {
        var operandResultArray = operandResults as T[] ?? operandResults.ToArray();
        var trueList = new List<T>();
        var falseList = new List<T>();
        foreach (var operand in operandResultArray)
            (satisfiedSelector(operand) ? trueList : falseList).Add(operand);

        List<T>? candidateCauses = isSatisfied switch
        {
            true when higherOrderPredicate(trueList) => trueList,
            true when higherOrderPredicate(falseList) => falseList,
            false when !higherOrderPredicate(trueList) && trueList.Count > 0 => trueList,
            false when !higherOrderPredicate(falseList) && falseList.Count > 0 => falseList,
            _ => null
        };

        if (candidateCauses is null or { Count: 0 })
            return operandResultArray;

        return candidateCauses;
    }
}
