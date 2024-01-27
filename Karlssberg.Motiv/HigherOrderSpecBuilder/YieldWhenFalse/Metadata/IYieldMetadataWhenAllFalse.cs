namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the metadata that is yielded when all elements
/// in a collection fail to satisfy the underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldMetadataWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata> : 
    IHigherOrderSpecFactory<TModel, TMetadata>
{
    /// <summary>Register a function that yields metadata for when all of the underlying boolean results are false.</summary>
    /// <param name="metadata">A function that receives the boolean results and returns the relevant metadata.</param>
    /// <returns>An interface to perform the next step in configuring the spec-builder.</returns>
    IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}