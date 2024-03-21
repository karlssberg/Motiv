using Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Explanation;
using Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders.Metadata;
namespace Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders;

/// <summary>
/// A builder for creating specifications using a predicate function that returns a <see cref="BooleanResultBase{TMetadata}"/>.
/// </summary>
/// <typeparam name="TModel">The type of the model the specification is for.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the metadata associated with the underlying boolean result.</typeparam>
public class TrueBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate)
{
    /// <summary>
    /// Specifies a reason why the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseMultiAssertionBooleanResultPredicateSpecBuilder{TModel,TUnderlyingMetadata}" />.</returns>
    public FalseAssertionWithPropositionBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) => 
        new(predicate,
            (_, _) => trueBecause,
            trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause)));

    /// <summary>
    /// Specifies a reason why the condition is true.
    /// </summary>
    /// <param name="trueBecause">A human-readable reason why the condition is true.</param>
    /// <returns>An instance of <see cref="FalseAssertionWithPropositionUnresolvedBooleanResultPredicateSpecBuilder{TModel}" />.</returns>
    public FalseMultiAssertionBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause)
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
    public FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue)
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
    public FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(Func<TModel, TMetadata> whenTrue)
    {
        whenTrue.ThrowIfNull(nameof(whenTrue));
        return new FalseMetadataBooleanResultPredicateSpecBuilder<TModel, TMetadata, TUnderlyingMetadata>(
            predicate,
            (model, _) => whenTrue(model).ToEnumerable());
    }

    /// <summary>
    /// Creates a specification and names it with the propositional statement provided.
    /// </summary>
    /// <param name="proposition">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>A specification for the model.</returns>
    public SpecBase<TModel, string> Create(string proposition) =>
        new BooleanResultPredicateMetadataSpec<TModel, string, TUnderlyingMetadata>(
            predicate,
            (_, _) => proposition,
            (_, _) => $"!{proposition}",
            proposition.ThrowIfNullOrWhitespace(nameof(proposition)));
}

