namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseAssertionFromBooleanResultWithNameHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    string trueBecause,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNameHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromBooleanResultWithNameHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNameHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromBooleanResultWithNameHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies assertions to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiAssertionExplanationFromBooleanResultWithNameHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public MultiAssertionExplanationFromBooleanResultWithNameHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalseYield(
        Func<HigherOrderBooleanResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);
}
