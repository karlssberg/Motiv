using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.FluentFactory.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The return type of the predicate expression.</typeparam>
[FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
public readonly partial struct TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(
    [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement) =>
        new MinimalHigherOrderFromExpressionTreeProposition<TModel, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true },
            higherOrderOperation.CauseSelector);

    /// <summary>Creates a proposition.</summary>
    /// <returns>A specification for the model.</returns>
    internal SpecBase<IEnumerable<TModel>, string> Create(Expression statement) =>
        new MinimalHigherOrderFromExpressionTreeProposition<TModel, TPredicateResult>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            new ExpressionAsStatementDescription(statement),
            higherOrderOperation.CauseSelector);
}
