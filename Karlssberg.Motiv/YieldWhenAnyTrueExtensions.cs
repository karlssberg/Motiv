using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IYieldFalseMetadata and IYieldFalseReasons interfaces. These methods allow
/// for the extension of the builder with functions that yield metadata when any of the models satisfy the underlying
/// specification.
/// </summary>
public static class YieldWhenAnyTrueExtensions
{
    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata,
        TUnderlyingMetadata>(
        this IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAnyTrue(results => [metadata(results)]);

    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata,
        TUnderlyingMetadata>(
        this IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAnyTrue(_ => metadata);

    /// <summary>
    /// Extends the builder with a function that yields a string reason when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TUnderlyingMetadata>(
        this IYieldAnyTrueReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> trueBecause) =>
        builder.YieldWhenAnyTrue(results => [trueBecause(results)]);

    /// <summary>
    /// Extends the builder with a function that yields a string reason when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <returns>The next set of builder operations.</returns>
    public static IYieldFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TUnderlyingMetadata>(
        this IYieldAnyTrueReasons<TModel, TUnderlyingMetadata> builder,
        string trueBecause) =>
        builder.YieldWhenAnyTrue(_ => trueBecause);
    
    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TAltMetadata">The type of the alternative metadata.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="converter">The converter to extend.</param>
    /// <param name="metadata">A function that takes a collection of BooleanResultWithModel and returns alternative metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TModel, TAltMetadata, TMetadata>(
        this IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> converter,
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, TAltMetadata> metadata) =>
        converter.YieldWhenAnyTrue<TAltMetadata>(results => [metadata(results)]);

    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TAltMetadata">The type of the alternative metadata.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="converter">The converter to extend.</param>
    /// <param name="metadata">A function that returns a collection of alternative metadata.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TModel, TAltMetadata, TMetadata>(
        this IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> converter,
        Func<IEnumerable<TAltMetadata>> metadata) =>
        converter.YieldWhenAnyTrue(results => metadata());

    /// <summary>
    /// Extends the builder with a function that yields metadata when any of the models satisfy the underlying
    /// specification.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TAltMetadata">The type of the alternative metadata.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <param name="converter">The converter to extend.</param>
    /// <param name="metadata">The alternative metadata to yield.</param>
    /// <returns>The next set of relevant builder operations.</returns>
    public static IYieldFalseMetadata<TModel, TAltMetadata, TMetadata> YieldWhenAnyTrue<TModel, TAltMetadata, TMetadata>(
        this IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata> converter,
        TAltMetadata metadata) =>
        converter.YieldWhenAnyTrue<TAltMetadata>(results => [metadata]);
}