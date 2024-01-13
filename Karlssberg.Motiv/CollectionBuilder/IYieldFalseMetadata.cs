namespace Karlssberg.Motiv.CollectionBuilder;

public interface IYieldFalseMetadata<TModel, TMetadata> : ISpecFactory<TModel, TMetadata> 
{
    ISpecFactory<TModel, TMetadata> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> yieldWhenAllFalse);
}