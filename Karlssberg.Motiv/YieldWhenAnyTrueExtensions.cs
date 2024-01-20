using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenFalse.Reasons;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv;

public static class YieldWhenAnyTrueExtensions
{
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAnyTrue(results => [metadata(results)]);

    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAnyTrue(_ => metadata);
    
    public static IYieldFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TUnderlyingMetadata>(
        this IYieldAnyTrueReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> metadata) =>
        builder.YieldWhenAnyTrue(results => [metadata(results)]);

    public static IYieldFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAnyTrue<TModel, TUnderlyingMetadata>(
        this IYieldAnyTrueReasons<TModel, TUnderlyingMetadata> builder,
        string metadata) =>
        builder.YieldWhenAnyTrue(_ => metadata);
    
}