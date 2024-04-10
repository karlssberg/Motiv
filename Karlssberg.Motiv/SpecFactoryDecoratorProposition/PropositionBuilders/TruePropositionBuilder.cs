using Karlssberg.Motiv.HigherOrderProposition;
using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders;
using Karlssberg.Motiv.SpecFactoryDecoratorProposition.PropositionBuilders.Explanation;
using Karlssberg.Motiv.SpecFactoryDecoratorProposition.PropositionBuilders.Metadata;

namespace Karlssberg.Motiv.SpecFactoryDecoratorProposition.PropositionBuilders;

/// <summary>
/// Represents a builder for creating propositions based on a predicate. This is particularly useful for handling edge-case scenarios where it would be impossible or impractical to create a proposition that covers every possibility, so instead it is done on a case-by-case basis.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata associated with the proposition.</typeparam>
public readonly ref struct TruePropositionBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> specPredicate)
{
    /// <summary>
    /// Specifies the metadata to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(specPredicate,
            (_, _) => whenTrue.ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(specPredicate,
            (model, _) => whenTrue(model).ToEnumerable());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue) =>
        new(specPredicate, whenTrue.ToEnumerableReturn());

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataPropositionBuilder{TModel,TMetadata,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<TMetadata>> whenTrue) =>
        new(specPredicate, whenTrue);

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithNamePropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionWithNamePropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        string trueBecause) =>
        new(specPredicate, trueBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, string> trueBecause) =>
        new(specPredicate, (model, _) => trueBecause(model));

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionPropositionBuilder<TModel, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause) =>
        new(specPredicate, trueBecause);

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseMetadataPropositionBuilder<TModel, string, TUnderlyingMetadata> WhenTrue(
        Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> trueBecause) =>
        new(specPredicate, trueBecause);


    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate) =>
        new(specPredicate.ToBooleanResultPredicate(),
            higherOrderPredicate);

    /// <summary>Specifies a higher order predicate for the proposition.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromSpecPropositionBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> As(
        Func<IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>,
            IEnumerable<BooleanResult<TModel, TUnderlyingMetadata>>> causeSelector) =>
        new(specPredicate.ToBooleanResultPredicate(),
            higherOrderPredicate,
            causeSelector);
    
    /// <summary>
    /// Creates a proposition and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A proposition for the model.</returns>
    public SpecBase<TModel, TUnderlyingMetadata> Create(string proposition) =>
        new SpecFactoryDecoratorMultiMetadataProposition<TModel, TUnderlyingMetadata, TUnderlyingMetadata>(
            specPredicate,
            (_, results) => results.Metadata,
            (_, results) => results.Metadata,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}