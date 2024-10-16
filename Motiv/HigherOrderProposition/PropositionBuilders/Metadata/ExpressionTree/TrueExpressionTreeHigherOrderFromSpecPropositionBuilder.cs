using System.Linq.Expressions;
using Motiv.HigherOrderProposition.BooleanResultPredicate;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Spec;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct TrueExpressionTreeHigherOrderFromSpecPropositionBuilder<TModel>(
    Expression<Func<TModel, bool>> expression,
    Func<IEnumerable<BooleanResult<TModel, string>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<BooleanResult<TModel, string>>, IEnumerable<BooleanResult<TModel, string>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseExpressionTreeMetadataHigherOrderPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(expression,
            higherOrderPredicate,
            whenTrue.ToFunc<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata>(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    /// <remarks>
    /// <para>
    /// If you wish to return a collection of metadata items, you will need to use the <c>WhenTrueYield()</c>
    /// method instead, otherwise the whole collection will become the metadata.
    /// </para>
    /// </remarks>
    public FalseExpressionTreeMetadataHigherOrderPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> whenTrue) =>
        new(expression,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies the set of metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseExpressionTreeMultiMetadataFromSpecHigherOrderPropositionBuilder<TModel, TMetadata> WhenTrueYield<TMetadata>(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<TMetadata>> whenTrue) =>
        new(expression,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause"> A human-readable reason why the condition is true. </param>
    /// <returns>
    /// An instance of <see cref="FalseAssertionFromSpecWithNameHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.
    /// </returns>
    public FalseExpressionTreeAssertionFromSpecWithNameHigherOrderPropositionBuilder<TModel> WhenTrue(
        string trueBecause)
    {
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new(
            expression,
            higherOrderPredicate,
            trueBecause,
            causeSelector);
    }

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFromBooleanResultHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseExpressionTreeAssertionFromBooleanResultHigherOrderPropositionBuilder<TModel> WhenTrue(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause) =>
        new(expression,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Specifies the set of assertions to use when the condition is true.</summary>
    /// <param name="whenTrue">A function that generates a collection of assertions when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseExpressionTreeMultiMetadataFromSpecHigherOrderPropositionBuilder<TModel, string> WhenTrueYield(
        Func<HigherOrderBooleanResultEvaluation<TModel, string>, IEnumerable<string>> whenTrue) =>
        new(expression,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement) =>
        new HigherOrderFromBooleanResultExpressionTreeProposition<TModel>(
            expression,
            higherOrderPredicate,
            new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))),
            causeSelector);
}
