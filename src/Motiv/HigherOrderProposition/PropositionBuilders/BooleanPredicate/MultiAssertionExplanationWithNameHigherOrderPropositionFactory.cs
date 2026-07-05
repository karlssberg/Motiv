using Converj.Attributes;
using Motiv.HigherOrderProposition.BooleanPredicate;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.BooleanPredicate;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <param name="resultResolver">The predicate to use for the specification.</param>
/// <param name="higherOrderOperation">The higher-order operation to use for the specification.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct MultiAssertionExplanationWithNameHigherOrderPropositionFactory<TModel>(
    [FluentMethod("Build")]Func<TModel, bool> resultResolver,
    [MultipleFluentMethods(typeof(HigherOrderBooleanPredicateSpecMethods))]HigherOrderSpecBooleanPredicateOperation<TModel> higherOrderOperation,
    [FluentMethod("WhenTrue")]string trueBecause,
    [FluentMethod("WhenFalseYield")]Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> falseBecause)
{
    private Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> TrueBecauseFunc =>
        trueBecause
            .ToEnumerable()
            .ToFunc<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>>();

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        resultResolver.ThrowIfNull(nameof(resultResolver));
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel, string>(
            resultResolver,
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
    public SpecBase<IEnumerable<TModel>, string> Create()
    {
        resultResolver.ThrowIfNull(nameof(resultResolver));
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition<TModel>(
            resultResolver,
            higherOrderOperation.HigherOrderPredicate,
            TrueBecauseFunc,
            falseBecause,
            new SpecDescription(trueBecause),
            higherOrderOperation.CauseSelector);
    }
}
