using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

/// <summary>
/// Provides methods that define metadata of a different type and yield it when all of the models satisfy the
/// underlying specification. This interface is used to convert the metadata type for the YieldWhenAllTrue method in the
/// higher order specification builder.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
public interface IYieldTypeConvertedMetadataWhenAllTrue<TModel, TUnderlyingMetadata>
{
    /// <summary>Registers a function that yields alternative metadata when all of the underlying boolean results are true.</summary>
    /// <typeparam name="TAltMetadata">The type of the alternative metadata.</typeparam>
    /// <param name="metadata">
    /// A function that takes an enumerable of boolean results with the model and returns an enumerable
    /// of alternative metadata.
    /// </param>
    /// <returns>
    /// An interface that allows the builder to restrict the set of operations to those that are permitted after
    /// defining the metadata to yield when false.
    /// </returns>
    IYieldMetadataWhenFalse<TModel, TAltMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TAltMetadata>(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TAltMetadata>> metadata);
}