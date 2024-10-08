using System.Linq.Expressions;

namespace Motiv.ExpressionTrees.PropositionBuilders.Explanation;

/// <summary>
/// A builder for creating propositions based on an lambda expression trees and explanation factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct FalseAssertionPropositionBuilder<TModel>(
    Expression<Func<TModel, bool>> expression,
    Func<TModel, BooleanResultBase<string>, string> trueBecause)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A human-readable reason why the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationPropositionFactory<TModel> WhenFalse(
        string falseBecause)
    {
        falseBecause.ThrowIfNullOrWhitespace(nameof(falseBecause));
        return new ExplanationPropositionFactory<TModel>(
            expression,
            trueBecause,
            (_, _) => falseBecause);
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied proposition and explanation factories.</returns>
    public ExplanationPropositionFactory<TModel> WhenFalse(
        Func<TModel, string> falseBecause)
    {
        return new ExplanationPropositionFactory<TModel>(
            expression,
            trueBecause,
            (model, _) => falseBecause(model));
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a human-readable reason when the condition is false.</param>
    /// <returns>A factory for creating propositions based on the supplied specification and explanation factories.</returns>
    public ExplanationPropositionFactory<TModel> WhenFalse(
        Func<TModel, BooleanResultBase<string>, string> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new ExplanationPropositionFactory<TModel>(
            expression,
            trueBecause,
            falseBecause);
    }

    /// <summary>
    /// Specifies an assertion to yield when the condition is false.
    /// </summary>
    /// <param name="falseBecause">A function that generates a collection of human-readable reasons when the condition is false.</param>
    /// <returns>A factory for creating specifications based on the supplied specification and explanation factories.</returns>
    public MultiAssertionExplanationPropositionFactory<TModel> WhenFalseYield(
        Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
    {
        falseBecause.ThrowIfNull(nameof(falseBecause));
        return new MultiAssertionExplanationPropositionFactory<TModel>(
            expression,
            trueBecause.ToEnumerableReturn(),
            falseBecause);
    }
}
