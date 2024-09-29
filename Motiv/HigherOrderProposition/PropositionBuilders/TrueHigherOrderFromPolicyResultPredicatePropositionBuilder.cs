using Motiv.HigherOrderProposition.PropositionBuilders.Explanation;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct TrueHigherOrderFromPolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, PolicyResultBase<TUnderlyingMetadata>> resultResolver,
    Func<IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<bool,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<PolicyResult<TModel, TUnderlyingMetadata>>> causeSelector)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataFromPolicyResultHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(resultResolver,
            higherOrderPredicate, _ => whenTrue,
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
    public FalseMetadataFromPolicyResultHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies the set of metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMultiMetadataFromPolicyResultHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrueYield<TMetadata>(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.  This will also be the name of the proposition, unless otherwise
    /// specified by the subsequent <c>Create(string statement)</c> method.</summary>
    /// <param name="trueBecause"> A human-readable reason why the condition is true. </param>
    /// <returns>
    /// An instance of <see cref="FalseAssertionFromSpecWithNameHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.
    /// </returns>
    public FalseAssertionFromPolicyResultWithNameHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFromPolicyResultHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionFromPolicyResultHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(resultResolver,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Specifies the set of metadata to use when the condition is true.</summary>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMultiMetadataFromPolicyResultHigherOrderSpecBuilder<TModel, string, TUnderlyingMetadata> WhenTrueYield(
        Func<HigherOrderPolicyResultEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<string>> whenTrue) =>
        new(resultResolver,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromPolicyResultMultiMetadataProposition<TModel, TUnderlyingMetadata, TUnderlyingMetadata>(
            resultResolver,
            higherOrderPredicate,
            eval => eval.Values,
            eval => eval.Values,
            new SpecDescription(statement),
            causeSelector);
    }
}
