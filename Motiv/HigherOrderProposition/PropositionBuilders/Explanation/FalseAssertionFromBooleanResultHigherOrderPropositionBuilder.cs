namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseAssertionFromBooleanResultHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiAssertionExplanationHigherOrderBooleanResultPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public MultiAssertionExplanationHigherOrderBooleanResultPropositionFactory<TModel, TUnderlyingMetadata> WhenFalseYield(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause,
            causeSelector);
}
