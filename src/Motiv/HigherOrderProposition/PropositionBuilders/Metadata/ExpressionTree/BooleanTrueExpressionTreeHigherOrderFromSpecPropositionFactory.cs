using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Converj.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;

/// <summary>
/// A builder for creating propositions based on a boolean predicate expression and explanations for true and false
/// conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or
/// impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <remarks>
/// This <c>bool</c>-fixed twin of the generic <c>TPredicateResult</c> factory exists solely so the
/// fluent <c>Spec.From(boolean-predicate)</c> overload chain continues into the higher-order builder
/// methods. Unlike the flat-proposition <c>Boolean*</c> factories in
/// <c>Motiv.ExpressionTreeProposition.PropositionBuilders</c>, it intentionally does not wrap its result
/// in an expression-backed type (<c>ExpressionSpecBase</c>/<c>ExpressionPolicyBase</c>) — higher-order
/// propositions aggregate over <c>IEnumerable&lt;TModel&gt;</c>, and expression-backing for them is
/// deliberately out of scope. Do not "fix" this asymmetry by wiring in decorators.
/// </remarks>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct BooleanTrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement) =>
        new MinimalHigherOrderFromExpressionTreeProposition<TModel, bool>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))),
            higherOrderOperation.CauseSelector);

    /// <summary>Creates a proposition.</summary>
    /// <returns>A specification for the model.</returns>
    internal SpecBase<IEnumerable<TModel>, string> Create(Expression statement) =>
        new MinimalHigherOrderFromExpressionTreeProposition<TModel, bool>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            new ExpressionAsStatementDescription(statement),
            higherOrderOperation.CauseSelector);
}
