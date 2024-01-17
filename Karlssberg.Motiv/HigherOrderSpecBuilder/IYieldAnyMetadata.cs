namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> : 
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnyTrueMetadataTypeConverter<TModel, TMetadata>,
    IYieldAnythingTypeConverter<TModel, TMetadata>
{
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnything(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
    
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}