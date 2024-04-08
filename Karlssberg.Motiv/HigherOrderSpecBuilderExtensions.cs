using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;
using Karlssberg.Motiv.SpecDecorator.SpecDecoratorSpecBuilders;

namespace Karlssberg.Motiv;

public static class HigherOrderSpecBuilderExtensions
{
    public static TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    public static TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    public static TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> AsAllSatisfied<TModel>(
            this BooleanPredicateSpecBuilder<TModel> builder) =>
            builder.As(results => results.All(tuple => tuple.Satisfied));
    
    
    public static TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    public static TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    public static  TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> AsAnySatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.Any(result => result.Satisfied));
    
    
    public static TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());
    public static TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());

    public static TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> AsNoneSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder) =>
        builder.As(booleanResults => booleanResults.All(result => !result.Satisfied));
    
    
    public static TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> AsNSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) == n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
    
    
    public static TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> AsAtLeastNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() >= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> AsAtLeastNSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) >= n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
    

    public static TrueHigherOrderFromUnderlyingSpecBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueSpecDecoratorSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this BooleanResultPredicateSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
    public static TrueHigherOrderFromBooleanPredicateSpecBuilder<TModel> AsAtMostNSatisfied<TModel>(
        this BooleanPredicateSpecBuilder<TModel> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.Count(result => result.Satisfied) <= n,
            (_, booleanResults) => booleanResults.Where(result => result.Satisfied));
    }
}