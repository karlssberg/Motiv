using Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders;
using Karlssberg.Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders;
using Karlssberg.Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if at least
/// a certain number of the underlying propositions are satisfied.
/// </summary>
public static class AsAtLeaseNSatisfiedExtensions
{
    /// <summary>
    /// Creates a higher order proposition that is satisfied if at least 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The minimum number of underlying propositions that need to be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if at least 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The minimum number of underlying propositions that need to be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>
        AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
            this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
            int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if at least 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The minimum number of underlying propositions that need to be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAtLeastNSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) >= n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .Where(result => result.Satisfied)
                    .ElseIfEmpty(booleanResultsArray);
            });
    }
}