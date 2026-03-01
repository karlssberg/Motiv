using System.Linq.Expressions;
using Motiv.FluentFactory.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TPredicateResult">The type of the underlying metadata associated with the specification.</typeparam>
public readonly struct MultiAssertionExplanationFromBooleanResultHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult>
{
    private readonly Expression<Func<TModel, TPredicateResult>> _expression;
    private readonly HigherOrderSpecPredicateOperation<TModel, string> _higherOrderOperation;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> _trueBecause;
    private readonly Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> _falseBecause;

    /// <summary>
    /// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="higherOrderOperation">The higher-order predicate operation.</param>
    /// <param name="trueBecause">The explanation for when the predicate is true.</param>
    /// <param name="falseBecause">The explanation for when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationFromBooleanResultHigherOrderExpressionTreePropositionFactory(
        [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> trueBecause,
        [MultipleFluentMethods(typeof(WhenFalseYieldOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> falseBecause)
    {
        _expression = expression;
        _higherOrderOperation = higherOrderOperation;
        _trueBecause = trueBecause;
        _falseBecause = falseBecause;
    }


    /// <summary>
    /// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="higherOrderOperation">The higher-order predicate operation.</param>
    /// <param name="trueBecause">The explanation for when the predicate is true.</param>
    /// <param name="falseBecause">The explanation for when the predicate is false.</param>
    [FluentConstructor(typeof(Motiv.Spec), Options = FluentOptions.NoCreateMethod)]
    public MultiAssertionExplanationFromBooleanResultHigherOrderExpressionTreePropositionFactory(
        [FluentMethod("From")]Expression<Func<TModel, TPredicateResult>> expression,
        [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
        [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
        [FluentMethod("WhenFalseYield", Priority = -1)]Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> falseBecause)
    {
        _expression = expression;
        _higherOrderOperation = higherOrderOperation;
        _trueBecause = trueBecause.ToEnumerableReturn();
        _falseBecause = falseBecause;
    }

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromExpressionTreeMultiMetadataProposition<TModel, string, TPredicateResult>(
            _expression,
            _higherOrderOperation.HigherOrderPredicate,
            _trueBecause,
            _falseBecause,
            new SpecDescription(statement) { HasExplicitStatement = true },
            _higherOrderOperation.CauseSelector);
    }
}
