﻿namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseAssertionFromPolicyResultHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause,
    Func<bool, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromPolicyResultHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(string falseBecause) =>
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
    public ExplanationFromPolicyResultHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalse(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, string> falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiAssertionExplanationFromBooleanResultHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public MultiAssertionExplanationFromPolicyResultHigherOrderPropositionFactory<TModel, TUnderlyingMetadata> WhenFalseYield(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> falseBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause,
            causeSelector);
}
