namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

public interface IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnyFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}