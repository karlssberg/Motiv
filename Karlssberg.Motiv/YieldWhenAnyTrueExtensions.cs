using Karlssberg.Motiv.HigherOrderSpecBuilder;

namespace Karlssberg.Motiv;

public static class YieldWhenAnyTrueExtensions 
{
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAnyTrue(results => [metadata(results)]);
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAnyTrue(_ => metadata());
    
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAnyTrue(() => [metadata]);
}