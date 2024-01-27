using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IYieldAnythingTypeConverter interface. These methods allow for the extension
/// of the builder with functions that yield metadata in response to the outcome of evaluating the models.
/// </summary>
public static class YieldExtensions
{
    /// <summary>
    /// Extends the builder with a function that yields metadata based on the outcome from evaluating the models.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that takes a boolean and a collection of BooleanResultWithModel and returns metadata.</param>
    /// <returns>The next set of builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldMetadataFromFactory<TModel, TUnderlyingMetadata> builder,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.Yield<TMetadata>((isSatisfied, results) => [metadata(isSatisfied, results)]);

    /// <summary>
    /// Extends the builder with a function that yields metadata based on the outcome from evaluating the models.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that takes a boolean and returns metadata.</param>
    /// <returns>The next set of builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldMetadataFromFactory<TModel, TUnderlyingMetadata> builder,
        Func<bool, TMetadata> metadata) =>
        builder.Yield<TMetadata>((isSatisfied, _) => [metadata(isSatisfied)]);

    /// <summary>
    /// Extends the builder with a function that yields metadata based on the outcome from evaluating the models.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that takes a boolean and a collection of BooleanResultWithModel and returns metadata.</param>
    /// <returns>The next set of builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldMetadataWhenTrue<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.Yield((isSatisfied, results) => [metadata(isSatisfied, results)]);
}