using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IYieldFalseMetadata and IYieldFalseReasons interfaces. These methods allow
/// for the extension of the builder with functions that yield metadata when any of the models do not satisfy the
/// underlying specification.
/// </summary>
public static class YieldWhenAnyFalseExtensions
{
    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models do not satisfy the underlying
    /// specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldMetadataWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TMetadata,
        TUnderlyingMetadata>(
        this IYieldMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata)
    {
        return builder.YieldWhenAnyFalse(results => [metadata(results)]);
    }

    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models do not satisfy the underlying
    /// specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldMetadataWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TMetadata,
        TUnderlyingMetadata>(
        this IYieldMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata)
    {
        return builder.YieldWhenAnyFalse(_ => metadata);
    }

    /// <summary>
    /// Extends the builder with a function that yields a string reason when any of the models do not satisfy the
    /// underlying specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldReasonsWhenAllFalse<TModel, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TUnderlyingMetadata>(
        this IYieldReasonsWhenFalse<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> falseBecause)
    {
        return builder.YieldWhenAnyFalse(results => [falseBecause(results)]);
    }

    /// <summary>
    /// Extends the builder with a function that yields a string reason when any of the models do not satisfy the
    /// underlying specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldReasonsWhenAllFalse<TModel, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TUnderlyingMetadata>(
        this IYieldReasonsWhenFalse<TModel, TUnderlyingMetadata> builder,
        string falseBecause)
    {
        return builder.YieldWhenAnyFalse(_ => falseBecause);
    }
}