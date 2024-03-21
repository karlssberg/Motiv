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
public readonly ref struct TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
        IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector = null)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(spec,
            higherOrderPredicate, _ => whenTrue.ToEnumerable(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            results => whenTrue(results).ToEnumerable(),
            causeSelector);

    /// <summary>Specifies the set of metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>
    /// An instance of <see cref="FalseAssertionsWithPropositionHigherOrderSpecBuilder{TModel,TUnderlyingMetadata}" />.
    /// </returns>
    public FalseAssertionsWithPropositionHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) =>
        new(spec,
            higherOrderPredicate,
            _ => trueBecause,
            trueBecause,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionsHigherOrderSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionsHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Creates a specification and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, string> Create(string proposition) =>
        new HigherOrderMetadataSpec<TModel, string, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            _ => proposition,
            _ => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
            causeSelector);
}