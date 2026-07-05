using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation to use when the expression evaluates to true.</param>
/// <param name="falseBecause">The explanation to use when the expression evaluates to false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [MultipleFluentMethods(typeof(WhenTrueLambdaOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, string> falseBecause)
{
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMetadataProposition<TModel, string, TPredicateResult>(
            expression,
            trueBecause,
            falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))));

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public PolicyBase<TModel, string> Create() =>
        new ExpressionTreeExplanationProposition<TModel, TPredicateResult>(
            expression,
            trueBecause,
            falseBecause,
            new ExpressionTreeDescription<TModel, TPredicateResult>(expression));
}
