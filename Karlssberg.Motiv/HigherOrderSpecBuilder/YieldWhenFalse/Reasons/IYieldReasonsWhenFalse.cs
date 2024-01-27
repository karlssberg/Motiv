namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

/// <summary>
/// Combines functionality from IYieldAllFalseReasons and IYieldAnyFalseReasons so that together they can be used
/// as a return type.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldReasonsWhenFalse<TModel, TUnderlyingMetadata> :
    IYieldReasonsWhenAllFalse<TModel, TUnderlyingMetadata>,
    IYieldReasonsWhenAnyFalse<TModel, TUnderlyingMetadata>
{
}