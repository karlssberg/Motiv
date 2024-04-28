using Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders;
using Karlssberg.Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders;
using Karlssberg.Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if any of the
/// underlying propositions are satisfied.
/// </summary>
public static class AsAnySatisfiedExtensions
{
    /// <summary>
    /// Creates a higher order proposition that is satisfied if any of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if any of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if any of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAnySatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.Any(result => result.Satisfied));
}