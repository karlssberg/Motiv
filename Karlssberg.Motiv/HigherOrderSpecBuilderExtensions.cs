using Karlssberg.Motiv.BooleanPredicateProposition.PropositionBuilders;
using Karlssberg.Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Karlssberg.Motiv.HigherOrderProposition.PropositionBuilders;
using Karlssberg.Motiv.SpecDecoratorProposition.PropositionBuilders;

namespace Karlssberg.Motiv;

public static class HigherOrderSpecBuilderExtensions
{
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAllSatisfied<TModel>(
            this BooleanPredicatePropositionBuilder<TModel> builder) =>
            builder.As(results => results.All(tuple => tuple.Satisfied));
    
    
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    
    public static  TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAnySatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.Any(result => result.Satisfied));
    
    
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());
    
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());

    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsNoneSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.All(result => !result.Satisfied));
    
    
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsNSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) == n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
    
    
    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAtLeastNSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) >= n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
    

    public static TrueHigherOrderFromSpecPropositionBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this TruePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    
    public static TrueHigherOrderFromBooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicatePropositionBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    
    public static TrueHigherOrderFromBooleanPredicatePropositionBuilder<TModel> AsAtMostNSatisfied<TModel>(
        this BooleanPredicatePropositionBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) <= n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
}