﻿using Karlssberg.Motiv.BooleanPredicateProposition;
using Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders.Explanation;
using Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders.Metadata;
using Karlssberg.Motiv.HigherOrderProposition;
using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// A builder for creating propositions based on a predicate, or for further refining a proposition.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
public readonly ref struct BooleanPredicatePropositionBuilder<TModel>(Func<TModel, bool> predicate)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithNamePropositionBuilder{TModel}" />.</returns>
    public FalseAssertionWithNamePropositionBuilder<TModel> WhenTrue(string trueBecause) =>
        new(predicate,
            trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionPropositionBuilder{TModel}" />.</returns>
    public FalseAssertionPropositionBuilder<TModel> WhenTrue(Func<TModel, string> trueBecause) =>
        new(predicate,
            trueBecause.ThrowIfNull(nameof(trueBecause)));

    /// <summary>
    /// Specifies the metadata to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataPropositionBuilder<TModel, TMetadata>(
            predicate,
            _ => whenTrue);
    }

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataPropositionBuilder<TModel, TMetadata>(predicate, whenTrue);
    }
    
    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> As(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate) =>
        new(predicate, higherOrderPredicate);

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> As(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector) =>
        new(predicate, higherOrderPredicate, causeSelector);

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new ExplanationProposition<TModel>(
            predicate,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}