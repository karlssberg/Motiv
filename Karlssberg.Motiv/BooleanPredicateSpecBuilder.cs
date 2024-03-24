using Karlssberg.Motiv.FirstOrder;
using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Explanation;
using Karlssberg.Motiv.FirstOrder.FirstOrderSpecBuilders.Metadata;
using Karlssberg.Motiv.HigherOrder;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// A builder for creating specifications based on a predicate, or for further refining a specification.
/// </summary>
/// <typeparam name="TModel">The type of the model the specification is for.</typeparam>
public readonly ref struct BooleanPredicateSpecBuilder<TModel>(Func<TModel, bool> predicate)
{
    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionFirstOrderSpecBuilder{TModel}" />.</returns>
    public FalseAssertionFirstOrderSpecBuilder<TModel> WhenTrue(string trueBecause) =>
        new(predicate,
            _ => trueBecause,
            trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));

    /// <summary>
    /// Specifies an assertion to yield when the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithPropositionUnresolvedFirstOrderSpecBuilder{TModel}" />.</returns>
    public FalseAssertionWithPropositionUnresolvedFirstOrderSpecBuilder<TModel> WhenTrue(Func<TModel, string> trueBecause) =>
        new(predicate,
            trueBecause.ThrowIfNull(nameof(trueBecause)));

    /// <summary>
    /// Specifies the metadata to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">The metadata to use when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataFirstOrderSpecBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(TMetadata whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata>(
            predicate,
            _ => whenTrue);
    }

    /// <summary>
    /// Specifies a metadata factory function to use when the condition is true.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to use when the condition is true.</typeparam>
    /// <param name="whenTrue">A function that generates a human-readable reason when the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMetadataFirstOrderSpecBuilder{TModel,TMetadata}" />.</returns>
    public FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataFirstOrderSpecBuilder<TModel, TMetadata>(predicate, whenTrue);
    }
    
    /// <summary>Specifies a higher order predicate for the specification.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromUnderlyingSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> As(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate) =>
        new(predicate, higherOrderPredicate);

    /// <summary>Specifies a higher order predicate for the specification.</summary>
    /// <param name="higherOrderPredicate">A function that takes a collection of boolean results and returns a boolean.</param>
    /// <param name="causeSelector">A function that selects the causes of the boolean results.</param>
    /// <returns>An instance of <see cref="TrueHigherOrderFromUnderlyingSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> As(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>>? causeSelector) =>
        new(predicate, higherOrderPredicate, causeSelector);

    /// <summary>
    /// Creates a specification and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new ExplanationSpec<TModel>(
            predicate,
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}