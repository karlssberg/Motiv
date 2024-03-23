﻿namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseAssertionsFromBooleanPredicateWithPropositionHigherOrderSpecBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<(TModel, bool)>, bool> higherOrderPredicate, 
    Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause,
    IProposition candidateProposition,
    Func<bool, IEnumerable<(TModel, bool)>, IEnumerable<(TModel, bool)>>? causeSelector)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithPropositionHigherOrderSpecFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromBooleanPredicateWithPropositionHigherOrderSpecFactory<TModel> WhenFalse(string falseBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause,
            candidateProposition,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithPropositionHigherOrderSpecFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromBooleanPredicateWithPropositionHigherOrderSpecFactory<TModel> WhenFalse(
        Func<HigherOrderBooleanEvaluation<TModel>, string> falseBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            candidateProposition,
            causeSelector);
}