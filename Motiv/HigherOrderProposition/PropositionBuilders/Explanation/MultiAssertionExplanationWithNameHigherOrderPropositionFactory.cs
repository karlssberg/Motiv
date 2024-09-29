using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation;

/// <summary>
/// A factory for creating specifications based on a predicate and explanations for true and false conditions. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct MultiAssertionExplanationWithNameHigherOrderPropositionFactory<TModel, TUnderlyingMetadata>(
    Func<TModel, bool> resultResolver,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    string trueBecause,
    Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> falseBecause,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel, string>(
            resultResolver,
            higherOrderPredicate,
            trueBecause
                .ToEnumerable()
                .ToFunc<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(statement),
            causeSelector);
    }

    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false. The propositional statement
    /// will be obtained from the .WhenTrue() assertion.
    /// </summary>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create() =>
        new HigherOrderFromBooleanPredicateMultiMetadataProposition<TModel, string>(
            resultResolver,
            higherOrderPredicate,
            trueBecause
                .ToEnumerable()
                .ToFunc<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>>(),
            falseBecause,
            new SpecDescription(trueBecause),
            causeSelector);
}
