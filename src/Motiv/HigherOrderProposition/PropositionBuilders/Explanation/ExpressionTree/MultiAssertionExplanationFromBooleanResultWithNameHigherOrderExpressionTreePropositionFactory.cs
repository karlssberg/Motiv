using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="expression">The expression to evaluate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate function.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionExplanationFromBooleanResultWithNameHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> falseBecause)
{
    private Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> TrueBecauseFunc =>
        trueBecause
            .ToEnumerable()
            .ToFunc<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>>();

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable. Because a name is supplied, the <c>WhenTrue</c>/<c>WhenFalse</c> values are surfaced via <see cref="BooleanResultBase{TMetadata}.Values"/>, not <see cref="BooleanResultBase.Assertions"/>.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(statement),
            higherOrderOperation.CauseSelector);
    }

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    /// <exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>
    public SpecBase<IEnumerable<TModel>, string> Create()
    {
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new HigherOrderFromExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(trueBecause),
            higherOrderOperation.CauseSelector);
    }
}
