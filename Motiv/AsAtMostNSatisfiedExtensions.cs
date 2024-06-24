using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if at most
/// a certain number of the underlying propositions are satisfied.
/// </summary>
public static class AsAtMostNSatisfiedExtensions
{
    /// <summary>
    /// Creates a higher order proposition that is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>
        AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
            this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
            int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAtMostNSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }
}
