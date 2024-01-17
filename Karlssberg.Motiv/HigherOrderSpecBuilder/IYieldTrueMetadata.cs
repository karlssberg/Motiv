namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IYieldTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> :  IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> 
{
    IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}