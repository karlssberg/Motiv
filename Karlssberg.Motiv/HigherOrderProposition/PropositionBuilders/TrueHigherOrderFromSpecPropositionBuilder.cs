﻿using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders.Explanation;
using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders.Metadata;

namespace Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating propositions based on a predicate and explanations for true and false conditions.
/// This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a
/// proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec,
    Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
    Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>>? causeSelector = null)
{
    /// <summary>Specifies the metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataHigherOrderPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(spec,
            higherOrderPredicate, 
            whenTrue
                .ToEnumerable()
                .ToFunc<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>>(),
            causeSelector);

    /// <summary>Specifies a metadata factory function to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataHigherOrderPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            results => whenTrue(results).ToEnumerable(),
            causeSelector);

    /// <summary>Specifies the set of metadata to use when the condition is true.</summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a collection of metadata when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataHigherOrderPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataHigherOrderPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(spec,
            higherOrderPredicate,
            whenTrue,
            causeSelector);

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>
    /// An instance of <see cref="FalseAssertionFromSpecDecoratorWithNameHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.
    /// </returns>
    public FalseAssertionFromSpecDecoratorWithNameHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause)
    {
        trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        return new FalseAssertionFromSpecDecoratorWithNameHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata>(
            spec,
            higherOrderPredicate,
            trueBecause,
            causeSelector);
    }

    /// <summary>Specifies an assertion to yield when the condition is true.</summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFromBooleanResultHigherOrderPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionFromBooleanResultHigherOrderPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<HigherOrderEvaluation<TModel, TUnderlyingMetadata>, string> trueBecause) =>
        new(spec.IsSatisfiedByWithExceptionRethrowing,
            higherOrderPredicate,
            trueBecause,
            causeSelector);

    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<IEnumerable<TModel>, TUnderlyingMetadata> Create(string proposition) =>
        new HigherOrderFromBooleanResultMultiMetadataProposition<TModel, TUnderlyingMetadata, TUnderlyingMetadata>(
            spec.ToBooleanResultPredicate(),
            higherOrderPredicate,
            eval => eval.Metadata,
            eval => eval.Metadata,
            new SpecDescription(
                proposition.ThrowIfNullOrWhitespace(nameof(proposition)),
                spec.Description.Statement),
            causeSelector);
}