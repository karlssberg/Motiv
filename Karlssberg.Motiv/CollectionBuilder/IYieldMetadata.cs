namespace Karlssberg.Motiv.CollectionBuilder;

public interface IYieldMetadata<TModel, TMetadata> :ISpecFactory<TModel, TMetadata> 
{
    IYieldFalseMetadata<TModel, TMetadata> YieldWhenAny(
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> yieldWhenAny);
}