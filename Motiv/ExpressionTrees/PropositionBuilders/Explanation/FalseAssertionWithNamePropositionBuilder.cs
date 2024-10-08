using System.Linq.Expressions;

namespace Motiv.ExpressionTrees.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions. This is
/// particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct FalseAssertionWithNamePropositionBuilder<TModel>(
    Expression<Func<TModel, bool>> expression,
    string trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel> WhenFalse(
        string falseBecause) =>
        new(expression,
            trueBecause,
            (_, _) => falseBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel> WhenFalse(
        Func<TModel, string> falseBecause) =>
        new(expression,
            trueBecause,
            (model, _) => falseBecause(model));

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="ExplanationWithNamePropositionFactory{TModel}" />.</returns>
    public ExplanationWithNamePropositionFactory<TModel> WhenFalse(
        Func<TModel, BooleanResultBase<string>, string> falseBecause) =>
        new(expression,
            trueBecause,
            falseBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>An instance of <see cref="MultiAssertionExplanationWithNamePropositionFactory{TModel}" />.</returns>
    public MultiAssertionExplanationWithNamePropositionFactory<TModel> WhenFalseYield(
        Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause) =>
        new(expression,
            trueBecause,
            falseBecause);
}
