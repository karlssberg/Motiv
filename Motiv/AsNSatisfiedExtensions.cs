using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.ExpressionTreeProposition.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Policy;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Spec;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if exactly
/// a certain number of the underlying propositions are satisfied.
/// </summary>
public static class AsNSatisfiedExtensions
{
    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    public static TrueHigherOrderFromPolicyPropositionBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePolicyBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            policyResults => policyResults.CountTrue() == n,
            (_, policyResults) =>
            {
                var policyResultsArray = policyResults.ToArray();
                return policyResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(policyResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<
        TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    public static TrueHigherOrderFromPolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<
        TModel, TUnderlyingMetadata>(
        this PolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            policyResults => policyResults.CountTrue() == n,
            (_, policyResults) =>
            {
                var policyResultsArray = policyResults.ToArray();
                return policyResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(policyResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsNSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition that is satisfied if exactly 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TPredicateResult">The return type of the predicate.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The exact number of underlying propositions that need to be satisfied.</param>
    /// <returns>A higher order proposition builder.</returns>
    public static TrueExpressionTreeHigherOrderFromSpecPropositionBuilder<TModel, TPredicateResult> AsNSatisfied<TModel, TPredicateResult>(
        this TrueExpressionTreePropositionBuilder<TModel, TPredicateResult> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) =>
            {
                var booleanResultsArray = booleanResults.ToArray();
                return booleanResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(booleanResultsArray);
            });
    }
}
