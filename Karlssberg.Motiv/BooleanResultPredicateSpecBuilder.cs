using Karlssberg.Motiv.BooleanResultPredicate;
using Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Explanation;
using Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Metadata;
using Karlssberg.Motiv.HigherOrder;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// A builder for creating propositions using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
/// </summary>
/// <typeparam name="TModel">The type of the model the proposition is for.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the metadata associated with the underlying boolean result.</typeparam>
public readonly ref struct BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMultiAssertionBooleanResultPredicateSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionWithPropositionBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) => 
        new(predicate,
            (_, _) => trueBecause,
            trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithPropositionUnresolvedBooleanResultPredicateSpecBuilder{TModel}" />.</returns>
    public FalseMultiAssertionBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, string> trueBecause)
    {
        trueBecause.ThrowIfNull(nameof(trueBecause));
        return new FalseMultiAssertionBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata>(predicate,
            (model, _) => trueBecause(model).ToEnumerable());
    }

    /// <summary>
    /// Specifies the metadata to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataBooleanResultPredicateSpecBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
            predicate,
            (_, _) => whenTrue.ToEnumerable());
    }

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataBooleanResultPredicateSpecBuilder{TModel, TMetadata, TUnderlyingMetadata}" />.</returns>
    public FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
            predicate,
            (model, _) => whenTrue(model).ToEnumerable());
    }
    
    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataBooleanResultPredicateSpecBuilder{TModel, TMetadata, TUnderlyingMetadata}" />.</returns>
    public FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>>  whenTrue) =>
        new(predicate, whenTrue.ThrowIfNull(nameof(whenTrue)));

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromUnderlyingSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        new(predicate, higherOrderPredicate);

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromUnderlyingSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>,
            IEnumerable<HigherOrder.BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector) =>
        new(predicate, higherOrderPredicate, causeSelector);

    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new BooleanResultPredicateMetadataSpec<TModel, string, TUnderlyingMetadata>(
            predicate,
            (_, _) => proposition,
            (_, _) => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}

