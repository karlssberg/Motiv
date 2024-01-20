namespace Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;

public interface IYieldAnyFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> : IHigherOrderSpecFactory<TModel, TMetadata>
{
    IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse(
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, IEnumerable<TMetadata>> metadata);
}