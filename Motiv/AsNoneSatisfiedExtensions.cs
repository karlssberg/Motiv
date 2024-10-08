using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.ExpressionTrees.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders;
using Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Motiv;

/// <summary>
/// Provides extension methods for proposition builders that create propositions that are satisfied if none of the
/// underlying propositions are satisfied.
/// </summary>
public static class AsNoneSatisfiedExtensions
{
    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromPolicyPropositionBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel,
        TUnderlyingMetadata>(
        this TruePolicyBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(policyResults => policyResults.AllFalse());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>
        AsNoneSatisfied<TModel, TUnderlyingMetadata>(
            this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromPolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata>
        AsNoneSatisfied<TModel, TUnderlyingMetadata>(
            this PolicyResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(policyResults => policyResults.AllFalse());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsNoneSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());

    /// <summary>
    /// Creates a higher order proposition that is satisfied if none of the underlying propositions are satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <param name="builder">The proposition builder.</param>
    /// <returns>The next build step.</returns>
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, string> AsNoneSatisfied<TModel>(
        this TrueExpressionTreePropositionBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());
}
