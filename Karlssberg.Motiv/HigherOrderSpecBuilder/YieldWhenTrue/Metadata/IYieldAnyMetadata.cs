using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

public interface IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata>
{
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}