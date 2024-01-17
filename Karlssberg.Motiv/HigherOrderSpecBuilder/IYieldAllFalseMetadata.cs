namespace Karlssberg.Motiv.HigherOrderSpecBuilder;

public interface IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> : IHigherOrderSpecFactory<TModel, TMetadata>
{
    IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}