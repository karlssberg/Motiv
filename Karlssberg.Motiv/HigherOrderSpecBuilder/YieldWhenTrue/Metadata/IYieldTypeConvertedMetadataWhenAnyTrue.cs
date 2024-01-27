using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

/// <summary>
/// Part of a fluent-builder interface that sets the metadata yielded when any elements in a collection meet the
/// underlying specification. This interface restricts operations to those allowed after defining the metadata for any true
/// yields.
/// </summary>
/// <typeparam name="TModel">Model type.</typeparam>
/// <typeparam name="TUnderlyingMetadata">Metadata type.</typeparam>
public interface IYieldTypeConvertedMetadataWhenAnyTrue<TModel, TUnderlyingMetadata>
{
    /// <summary>Registers a function that yields metadata when any boolean results with the model are true.</summary>
    /// <typeparam name="TAltMetadata">Alternative metadata type.</typeparam>
    /// <param name="metadata">A function that maps a collection of results to a collection of metadata.</param>
    /// <returns>The next set of builder operations.</returns>
    IYieldMetadataWhenFalse<TModel, TAltMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata);
}