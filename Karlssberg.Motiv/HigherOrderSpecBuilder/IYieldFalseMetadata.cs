namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnyFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}