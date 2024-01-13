namespace Karlssberg.Motiv.CollectionBuilder;

public interface IYieldTrueMetadata<TModel, TMetadata> :  IYieldMetadata<TModel, TMetadata> 
{
    IYieldMetadata<TModel, TMetadata> YieldWhenAllTrue(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> yieldWhenAllTrue);
}