using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Motiv.FluentFactory.Attributes;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation to use when the expression evaluates to true.</param>
/// <param name="falseBecause">The explanation to use when the expression evaluates to false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
[FluentConstructor(typeof(Spec), Options = FluentOptions.NoCreateMethod)]
public readonly struct ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [FluentMethod("WhenTrue")]string trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition with descriptive assertions, but using the supplied proposition to succinctly explain
    /// the decision.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create(string statement) =>
        new ExpressionTreeWithSingleTrueAssertionProposition<TModel, TPredicateResult>(
            expression,
            trueBecause,
            falseBecause,
            new SpecDescription(
                statement.ThrowIfNullOrWhitespace(nameof(statement))));

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create() =>
        new ExpressionTreeWithSingleTrueAssertionProposition<TModel, TPredicateResult>(
            expression,
            trueBecause,
            falseBecause,
            new ExpressionTreeDescription<TModel, TPredicateResult>(expression));
}
