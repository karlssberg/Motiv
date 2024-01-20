namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

public interface IYieldFalseReasons<TModel, TUnderlyingMetadata> :
    IYieldAllFalseReasons<TModel, TUnderlyingMetadata>,
    IYieldAnyFalseReasons<TModel, TUnderlyingMetadata>
{
}