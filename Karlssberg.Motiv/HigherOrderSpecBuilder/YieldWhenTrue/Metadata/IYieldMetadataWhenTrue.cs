namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

/// <summary>
/// Part of a fluent-builder interface that sets the metadata yielded when all elements in a collection meet the
/// underlying specification. This interface merges the functionalities of IYieldAnyTrueMetadata and
/// IYieldAllTrueMetadataTypeConverter.
/// </summary>
/// <typeparam name="TModel">Model type.</typeparam>
/// <typeparam name="TMetadata">Metadata type.</typeparam>
/// <typeparam name="TUnderlyingMetadata">Underlying metadata type.</typeparam>
public interface IYieldMetadataWhenTrue<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldMetadataWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldTypeConvertedMetadataWhenAllTrue<TModel, TUnderlyingMetadata>
{
    /// <summary>Registers a function that yields metadata when all boolean results with the model are true.</summary>
    /// <param name="metadata">A function that maps a collection of results to a collection of metadata.</param>
    /// <returns>An instance of IYieldAnyTrueMetadataOrFalseMetadata configured with the specified metadata yield function.</returns>
    IYieldMetadataWhenAnyTrueOrFalse<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);

    /// <summary>Registers a function that yields a specific type of metadata when the boolean result with the model is true.</summary>
    /// <param name="metadata">A function that maps a collection of results to a collection of metadata.</param>
    /// <returns>An instance of IHigherOrderSpecFactory that provides methods to create the specification.</returns>
    IHigherOrderSpecFactory<TModel, TMetadata> Yield(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}