namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the metadata for any false yield in the higher
/// order specification builder..
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldMetadataWhenAnyFalse<TModel, TMetadata, TUnderlyingMetadata> :
    IHigherOrderSpecFactory<TModel, TMetadata>
{
    /// <summary>Register a function that yields metadata for when any of the boolean results are false.</summary>
    /// <param name="metadata">A function that receives the boolean results and returns the relevant metadata.</param>
    /// <returns>The next set of builder operations.</returns>
    IYieldMetadataWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}