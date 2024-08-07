using Motiv.HigherOrderProposition.PropositionBuilders.Explanation;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly ref struct TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataFromBooleanHigherOrderPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(predicate,
            higherOrderPredicate,
            _ => whenTrue,
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
    public FalseMetadataFromBooleanHigherOrderPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue) =>
        new(predicate,
            higherOrderPredicate,
            whenTrue,
            causeSelector);


    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMultiMetadataFromBooleanHigherOrderPropositionBuilder<TModel, TMetadata> WhenTrueYield<TMetadata>(
        Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<TMetadata>> whenTrue) =>
        new(predicate,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.  This will also be the name of the proposition, unless otherwise
    /// specified by the subsequent <c>Create(string statement)</c> method.
    /// </summary>
    /// <param name="metadata">the metadata to yield when the condition is true.</param>
    /// <returns>
    /// An instance of <see cref="FalseAssertionFromSpecWithNameHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.
    /// </returns>
    public FalseAssertionFromBooleanPredicateWithNameHigherOrderPropositionBuilder<TModel> WhenTrue(
        string metadata) =>
        new(predicate,
            higherOrderPredicate,
            metadata,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFromBooleanResultHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionFromBooleanPredicateHigherOrderPropositionBuilder<TModel> WhenTrue(
        Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a collection of reasons when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFromBooleanResultHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseMultiMetadataFromBooleanHigherOrderPropositionBuilder<TModel, string> WhenTrueYield(
        Func<HigherOrderBooleanEvaluation<TModel>, IEnumerable<string>> trueBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromBooleanPredicateMetadataProposition<TModel, string>(
            predicate,
            higherOrderPredicate,
            _ => statement,
            _ => $"¬{statement}",
            new SpecDescription(statement),
            causeSelector);
    }
}
