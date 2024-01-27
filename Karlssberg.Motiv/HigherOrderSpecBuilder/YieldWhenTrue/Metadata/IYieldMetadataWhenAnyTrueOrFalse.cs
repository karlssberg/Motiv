using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

/// <summary>
/// Represents a part of a fluent-builder interface that configures the metadata that is yielded when any elements
/// in a collection either satisfy or fail to satisfy the underlying specification. This interface combines the
/// functionalities of IYieldAnyTrueMetadata and IYieldFalseMetadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldMetadataWhenAnyTrueOrFalse<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldMetadataWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldMetadataWhenFalse<TModel, TMetadata, TUnderlyingMetadata>
{
}