using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

/// <summary>
/// An interface that represents the beginning stage of a fluent-builder that configures the metadata that is
/// yielded when all elements in a collection satisfy the underlying specification. This interface combines the
/// functionalities of IYieldAllTrueMetadata, IYieldAnythingTypeConverter, and IYieldAnyTrueMetadataOrFalseMetadata.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IHigherOrderMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata>,
    IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}

/// <summary>
/// An interface that represents the beginning stage of a fluent-builder that configures the reasons that are
/// yielded when all elements in a collection satisfy the underlying specification. This interface combines the
/// functionalities of IYieldAllTrueReasons and IYieldAnyTrueReasonsOrFalseReasons.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IHigherOrderReasonsSpecBuilder<TModel, TUnderlyingMetadata> :
    IYieldAllTrueReasons<TModel, TUnderlyingMetadata>,
    IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata>
{
}