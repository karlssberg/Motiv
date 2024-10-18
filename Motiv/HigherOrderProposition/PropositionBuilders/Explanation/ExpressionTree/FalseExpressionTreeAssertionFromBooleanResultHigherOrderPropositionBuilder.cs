using System.Linq.Expressions;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct FalseExpressionTreeAssertionFromBooleanResultHigherOrderPropositionBuilder<TModel, TPredicateResult>(
    Expression<Func<TModel, TPredicateResult>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationHigherOrderExpressionTreePropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(string falseBecause) =>
        new(expression,
            higherOrderPredicate,
            trueBecause,
            _ => falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationHigherOrderExpressionTreePropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public ExplanationHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> falseBecause) =>
        new(expression,
            higherOrderPredicate,
            trueBecause,
            falseBecause,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiAssertionExplanationFromBooleanResultHigherOrderPropositionFactory{TModel,TUnderlyingMetadata}" />.</returns>
    public MultiAssertionExplanationFromBooleanResultHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalseYield(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> falseBecause) =>
        new(expression,
            higherOrderPredicate,
            trueBecause.ToEnumerableReturn(),
            falseBecause,
            causeSelector);
}
