namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the metadata that is yielded when elements in
/// a collection satisfy the underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata>
{
    /// <summary>
    /// Registers a function that yields relevant metadata when a collection of models are evaluated to be either true
    /// or false.
    /// </summary>
    /// <typeparam name="TMetadata">The type of the metadata to yield.</typeparam>
    /// <param name="metadata">A function that maps a collection of results to a collection of metadata.</param>
    /// <returns>An instance of IHigherOrderSpecFactory configured with the specified metadata yield function.</returns>
    IHigherOrderSpecFactory<TModel, TMetadata> Yield<TMetadata>(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}