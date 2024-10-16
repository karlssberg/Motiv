using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.ExpressionTrees.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Policy;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Spec;
using Motiv.Shared;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if at most
/// a certain number of the underlying propositions are satisfied.
/// </summary>
public static class AsAtMostNSatisfiedExtensions
{
    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
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
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromPolicyPropositionBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePolicyBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            policyResults => policyResults.CountTrue() <= n,
            (_, policyResults) =>
            {
                var policyResultsArray = policyResults.ToArray();
                return policyResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(policyResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
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
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromPolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>
        AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
            this PolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
            int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            policyResults => policyResults.CountTrue() <= n,
            (_, policyResults) =>
            {
                var policyResultsArray = policyResults.ToArray();
                return policyResultsArray
                    .WhereTrue()
                    .ElseIfEmpty(policyResultsArray);
            });
    }

    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
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

    /// <summary>
    /// Creates a higher order proposition which is satisfied if at most 'n' of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <param name="n">The maximum number of underlying propositions that can be satisfied.</param>
    /// <returns>The next build step.</returns>
    public static TrueExpressionTreeHigherOrderFromSpecPropositionBuilder<TModel> AsAtMostNSatisfied<TModel>(
        this TrueExpressionTreePropositionBuilder<TModel> builder,
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
