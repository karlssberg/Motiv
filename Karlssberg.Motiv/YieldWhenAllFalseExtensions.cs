using Karlssberg.Motiv.HigherOrderSpecBuilder;

namespace Karlssberg.Motiv;

public static class YieldWhenAllFalseExtensions
{
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAllFalse(results => [metadata(results)]);
    
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAllFalse(_ => metadata());
    
    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAllFalse(() => [metadata]);
}