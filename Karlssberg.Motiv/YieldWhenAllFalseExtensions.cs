using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IYieldFalseMetadata and IYieldFalseReasons interfaces. These methods allow
/// for the creation of higher order specification factories that yield metadata when all of the models do not satisfy the
/// underlying specification.
/// </summary>
public static class YieldWhenAllFalseExtensions
{
    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that takes a collection of BooleanResultWithModel and returns metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAllFalse(results => [metadata(results)]);

    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that returns a collection of metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAllFalse(_ => metadata());

    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that returns metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<TMetadata> metadata) =>
        builder.YieldWhenAllFalse(_ => metadata());

    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">The metadata to yield.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAllFalse(() => [metadata]);

    /// <summary>Creates a higher order specification factory that yields a string reason when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="falseBecause">A function that takes a collection of BooleanResultWithModel and returns a string reason.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> falseBecause) =>
        builder.YieldWhenAllFalse(results => [falseBecause(results)]);

    /// <summary>Creates a higher order specification factory that yields a string reason when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="falseBecause">A function that returns a collection of string reasons.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<string>> falseBecause) =>
        builder.YieldWhenAllFalse(_ => falseBecause());

    /// <summary>Creates a higher order specification factory that yields a string reason when all conditions are false.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="falseBecause">The string reason to yield.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        string falseBecause) =>
        builder.YieldWhenAllFalse(() => [falseBecause]);
}