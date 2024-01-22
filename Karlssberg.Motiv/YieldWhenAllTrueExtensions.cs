using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IYieldAllTrueMetadata and IYieldAllTrueReasons interfaces. These methods allow
/// for the creation of higher order specification factories that yield metadata when all of the models satisfy
/// the underlying specification.
/// </summary>
public static class YieldWhenAllTrueExtensions
{
    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are true.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that takes a collection of BooleanResultWithModel and returns metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel,
        TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAllTrue(results => [metadata(results)]);

    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are true.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">A function that returns a collection of metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel,
        TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAllTrue(_ => metadata());

    /// <summary>Creates a higher order specification factory that yields metadata when all conditions are true.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="metadata">The metadata to yield.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel,
        TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAllTrue(() => [metadata]);

    /// <summary>Creates a higher order specification factory that yields a string reason when all conditions are true.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="trueBecause">A function that takes a collection of BooleanResultWithModel and returns a string reason.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue<TModel,
        TUnderlyingMetadata>(
        this IYieldAllTrueReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> trueBecause) =>
        builder.YieldWhenAllTrue(results => [trueBecause(results)]);

    /// <summary>Creates a higher order specification factory that yields a string reason when all conditions are true.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="trueBecause">A function that returns a collection of string reasons.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue<TModel,
        TUnderlyingMetadata>(
        this IYieldAllTrueReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<string>> trueBecause) =>
        builder.YieldWhenAllTrue(_ => trueBecause());

    /// <summary>Creates a higher order specification factory that yields a string reason when all conditions are true.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="trueBecause">The string reason to yield.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue<TModel,
        TUnderlyingMetadata>(
        this IYieldAllTrueReasons<TModel, TUnderlyingMetadata> builder,
        string trueBecause) =>
        builder.YieldWhenAllTrue(_ => trueBecause);
}