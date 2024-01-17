using Karlssberg.Motiv.HigherOrderSpecBuilder;

namespace Karlssberg.Motiv;

public static class YieldWhenAllTrueExtensions
{
    public static IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAllTrue(results => [metadata(results)]);
    public static IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAllTrue(_ => metadata());
    
    public static IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAllTrue(() => [metadata]);
}