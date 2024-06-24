using System.Collections;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct FalseAssertionFromBooleanPredicateWithNameHigherOrderPropositionBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate, 
    string trueBecause,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNameHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromBooleanPredicateWithNameHigherOrderPropositionFactory<TModel> WhenFalse(string falseBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNameHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationFromBooleanPredicateWithNameHigherOrderPropositionFactory<TModel> WhenFalse(
        Func<HigherOrderBooleanEvaluation<TModel>, string> falseBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);
    
    /// <summary>
    /// Specifies assertions to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNameHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public MultiAssertionExplanationWithNameHigherOrderPropositionFactory<TModel, string> WhenFalseYield(
        Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> falseBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);
}