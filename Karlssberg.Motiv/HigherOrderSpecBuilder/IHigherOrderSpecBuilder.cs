using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IHigherOrderMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata>,
    IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}

public interface IHigherOrderReasonsSpecBuilder<TModel, TUnderlyingMetadata> :
    IYieldAllTrueReasons<TModel, TUnderlyingMetadata>,
    IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata>
{
}