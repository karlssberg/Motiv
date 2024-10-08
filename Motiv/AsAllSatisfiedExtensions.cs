using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.ExpressionTrees.PropositionBuilders;
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
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
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
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromPolicyPropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePolicyBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(policyResults => policyResults.AllTrue());

    /// <summary>
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());

    /// <summary>
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromPolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this PolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(policyResults => policyResults.AllTrue());

    /// <summary>
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAllSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder) =>
        builder.As(results => results.AllTrue());

    /// <summary>
    /// Builds a proposition which is satisfied if all the underlying propositions are satisfied.
    /// </summary>
    /// <param name="builder">The previous build step.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, string> AsAllSatisfied<TModel>(
        this TrueExpressionTreePropositionBuilder<TModel> builder) =>
        builder.As(results => results.AllTrue());
}
