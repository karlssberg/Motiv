namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

/// <summary>
/// Combines functionality from IYieldAllFalseMetadata and IYieldAnyFalseMetadata so that together it can be used
/// as a return type.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the underlying metadata.</typeparam>
public interface IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> :
    IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>,
    IYieldAnyFalseMetadata<TModel, TMetadata, TUnderlyingMetadata>
{
}