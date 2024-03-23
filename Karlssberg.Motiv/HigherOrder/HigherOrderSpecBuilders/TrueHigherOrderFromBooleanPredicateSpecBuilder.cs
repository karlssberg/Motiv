using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Explanation;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders.Metadata;

namespace Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

/// <summary>
/// A builder for creating specifications based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// specification that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the specification.</typeparam>
public readonly ref struct TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel>(
    Func<TModel, bool> predicate,
    Func<IEnumerable<(TModel, bool)>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<(TModel, bool)>, IEnumerable<(TModel, bool)>>? causeSelector = null)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataFromBooleanHigherOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(predicate,
            higherOrderPredicate, 
            _ => whenTrue,
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataFromBooleanHigherOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue) =>
        new(predicate,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>
    /// An instance of <see cref="FalseAssertionsWithPropositionHigherOrderSpecBuilder{TModel,TUnderlyingMetadata}" />.
    /// </returns>
    public FalseAssertionsFromBooleanPredicateWithPropositionHigherOrderSpecBuilder<TModel> WhenTrue(
        string trueBecause) =>
        new(predicate,
            higherOrderPredicate,
            _ => trueBecause,
            new Proposition(trueBecause),
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionsHigherOrderSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionsFromBooleanPredicateHigherOrderSpecBuilder<TModel> WhenTrue(
        Func<HigherOrderBooleanEvaluation<TModel>, string> trueBecause) =>
        new(predicate,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string proposition)
    {
        proposition.ThrowIfNullOrWhitespace(nameof(proposition));
        return new HigherOrderFromBooleanPredicateMetadataSpec<TModel, string>(
            predicate,
            higherOrderPredicate,
            _ => proposition,
            _ => $"!{proposition}",
            new Proposition(proposition),
                causeSelector);
    }
}