using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.SpecBuilder.Phase2;

namespace Karlssberg.Motiv;

/// <summary>
/// Provides extension methods for the IRequireFalseMetadata interface. These methods allow for the extension of the builder with functions
/// that yield metadata when the underlying specification is satisfied.
/// </summary>
public static class YieldWhenTrueExtensions
{
    /// <summary>
    /// Extends the builder with a function that yields metadata when the underlying specification is satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TNewMetadata">The type of the new metadata.</typeparam>
    /// <param name="spec">The specification to extend.</param>
    /// <param name="metadata">The metadata to yield.</param>
    /// <returns>The next set of builder operations.</returns>
    public static IRequireFalseMetadata<TModel, TNewMetadata> YieldWhenTrue<TModel, TMetadata, TNewMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        TNewMetadata metadata) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(metadata);

    /// <summary>
    /// Extends the builder with a function that yields metadata when the underlying specification is satisfied.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <typeparam name="TNewMetadata">The type of the new metadata.</typeparam>
    /// <param name="spec">The specification to extend.</param>
    /// <param name="metadata">A function that takes a model and returns the metadata to yield.</param>
    /// <returns>The next set of builder operations.</returns>
    public static IRequireFalseMetadata<TModel, TNewMetadata> YieldWhenTrue<TModel, TMetadata, TNewMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TModel, TNewMetadata> metadata) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(metadata);
}