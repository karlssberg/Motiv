namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IHigherOrderSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldTrueMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnyFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}