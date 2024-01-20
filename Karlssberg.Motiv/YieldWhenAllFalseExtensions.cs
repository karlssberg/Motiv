using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;

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
        Func<TMetadata> metadata) =>
        builder.YieldWhenAllFalse(_ => metadata());

    public static IHigherOrderSpecFactory<TModel, TMetadata> YieldWhenAllFalse<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAllFalse(() => [metadata]);
    
    
    
    public static IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> metadata) =>
        builder.YieldWhenAllFalse(results => [metadata(results)]);

    public static IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<string>> metadata) =>
        builder.YieldWhenAllFalse(_ => metadata());

    public static IHigherOrderSpecFactory<TModel, string> YieldWhenAllFalse<TModel, TUnderlyingMetadata>(
        this IYieldFalseReasons<TModel, TUnderlyingMetadata> builder,
        string metadata) =>
        builder.YieldWhenAllFalse(() => [metadata]);
}