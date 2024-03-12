using Karlssberg.Motiv.Composite.CompositeSpecBuilders;
using Karlssberg.Motiv.HigherOrder.HigherOrderSpecBuilders;

namespace Karlssberg.Motiv;

public static class HigherOrderSpecBuilderExtensions
{
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAllSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllTrue());
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAnySatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AnyTrue());
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsNoneSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder) =>
        builder.As(booleanResults => booleanResults.AllFalse());
    
    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() == n,
            (_, booleanResults) => booleanResults.WhereTrue());
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

    public static TrueHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> AsAtMostNSatisfied<TModel, TUnderlyingMetadata>(
        this TrueCompositeSpecBuilder<TModel, TUnderlyingMetadata> builder,
        int n)
    {
        n.ThrowIfLessThan(0, nameof(n));
        return builder.As(
            booleanResults => booleanResults.CountTrue() <= n,
            (_, booleanResults) => booleanResults.WhereTrue());
    }
}