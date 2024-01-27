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
public interface IHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldMetadataWhenTrue<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldMetadataFromFactory<TModel, TUnderlyingMetadata>,
    IYieldMetadataWhenAnyTrueOrFalse<TModel, TMetadata, TUnderlyingMetadata>
{
}

/// <summary>
/// An interface that represents the beginning stage of a fluent-builder that configures the reasons that are
/// yielded when all elements in a collection satisfy the underlying specification. This interface combines the
/// functionalities of IYieldAllTrueReasons and IYieldAnyTrueReasonsOrFalseReasons.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IHigherOrderSpecBuilder<TModel, TUnderlyingMetadata> :
    IYieldReasonsWhenAllTrue<TModel, TUnderlyingMetadata>,
    IYieldReasonsWhenAnyTrueOrFalse<TModel, TUnderlyingMetadata>
{
}