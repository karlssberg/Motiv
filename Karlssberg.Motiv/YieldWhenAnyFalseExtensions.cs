using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

namespace Karlssberg.Motiv;

public static class YieldWhenAnyFalseExtensions
{
    public static IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAnyFalse(results => [metadata(results)]);
    
    public static IYieldAllFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAnyFalse(_ => metadata);
    
    
    
    public static IYieldAllFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> metadata) =>
        builder.YieldWhenAnyFalse(results => [metadata(results)]);

    public static IYieldAllFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        string metadata) =>
        builder.YieldWhenAnyFalse(_ => metadata);
}