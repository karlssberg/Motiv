using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the metadata that is yielded when any elements
/// in a collection satisfy the underlying specification.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface
    IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata>
{
    /// <summary>Registers a function that yields metadata when any of the underlying boolean results are true.</summary>
    /// <param name="metadata">A function that receives the boolean results and returns the relevant metadata.</param>
    /// <returns>The next set of builder operations.</returns>
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}