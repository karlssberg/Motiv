using Karlssberg.Motiv.BooleanResultPredicate.BooleanResultPredicateBuilders;
using Karlssberg.Motiv.Composite.CompositeSpecBuilders;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv;

public static class HigherOrderSpecBuilderExtensions
{
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    public static TrueHigherOrderFromBooleanResultSpecBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    public static TrueHigherOrderFromBooleanSpecBuilder<TModel> AsAllSatisfied<TModel>(
            this BooleanPredicateSpecBuilder<TModel> builder) =>
            builder.As(results => results.All(tuple => tuple.Satisfied));
    
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    public static TrueHigherOrderFromBooleanResultSpecBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    public static  TrueHigherOrderFromBooleanSpecBuilder<TModel> AsAnySatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.Any(result => result.Satisfied));
    
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());
    public static TrueHigherOrderFromBooleanResultSpecBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());

    public static TrueHigherOrderFromBooleanSpecBuilder<TModel> AsNoneSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.All(result => !result.Satisfied));
    
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanResultSpecBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanSpecBuilder<TModel> AsNSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) == n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
    
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanResultSpecBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanSpecBuilder<TModel> AsAtLeastNSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) >= n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
    

    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanResultSpecBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanSpecBuilder<TModel> AsAtMostNSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) <= n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
}