using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if all the
/// underlying propositions are satisfied.
/// </summary>
public static class AsAllSatisfiedExtensions
{
    
    /// <summary>
    /// Converts a <see cref="TruePropositionBuilder{TModel,TUnderlyingMetadata}" /> into a proposition that is
    /// satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());

    /// <summary>
    /// Converts a <see cref="BooleanResultPredicatePropositionBuilder{TModel,TUnderlyingMetadata}" /> into a
    /// proposition that is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    
    /// <summary>
    /// Converts a <see cref="BooleanResultPredicatePropositionBuilder{TModel,TUnderlyingMetadata}" /> into a
    /// proposition that is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAllSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder) =>
        builder.As(results => results.All(tuple => tuple.Satisfied));

}