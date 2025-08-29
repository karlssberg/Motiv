using System.Linq.Expressions;
using Motiv.Generator.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;
using SpecDescription = Motiv.ExpressionTreeProposition.SpecDescription;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="expression">The expression to evaluate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The predicate type.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct ExplanationHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> falseBecause)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public PolicyBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromExpressionTreeExplanationProposition<TModel, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause,
            falseBecause,
            new SpecDescription(statement),
            higherOrderOperation.CauseSelector);
    }
}
