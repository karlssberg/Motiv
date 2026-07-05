using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating propositions based on the supplied proposition and explanation factories.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<TModel, BooleanResultBase<string>, IEnumerable<string>> falseBecause)
{
    private Func<TModel, BooleanResultBase<string>, IEnumerable<string>> TrueBecauseFunc =>
        trueBecause
            .ToEnumerable()
            .ToFunc<TModel, BooleanResultBase<string>, IEnumerable<string>>();

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string statement) =>
        new ExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            expression,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))));

    /// <summary>
    /// Creates a proposition with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create() =>
        new ExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>(
            expression,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause))));
}
