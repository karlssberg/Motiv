namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

public interface IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAllTrueMetadataTypeConverter<TModel, TMetadata>
{
    IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);

    IHigherOrderSpecFactory<TModel, TMetadata> Yield(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}