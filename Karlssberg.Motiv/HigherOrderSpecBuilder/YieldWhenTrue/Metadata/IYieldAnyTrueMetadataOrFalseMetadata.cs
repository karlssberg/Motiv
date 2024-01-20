using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

public interface IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}