using System.Linq.Expressions;

namespace Motiv.ExpressionTrees.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct ExplanationWithNamePropositionFactory<TModel>(
    Expression<Func<TModel, bool>> expression,
    string trueBecause,
    Func<TModel, BooleanResultBase<string>, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create() =>
        new ExpressionTreeWithSingleTrueAssertionProposition<TModel>(
            expression,
            trueBecause,
            falseBecause);

    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create(string statement) =>
        new ExpressionTreeWithSingleTrueAssertionProposition<TModel>(
            expression,
            trueBecause,
            falseBecause,
            statement.ThrowIfNullOrWhitespace(nameof(statement)));
}
