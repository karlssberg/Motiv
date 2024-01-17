using Karlssberg.Motiv.HigherOrderSpecBuilder;

namespace Karlssberg.Motiv;

public static class YieldWhenAnythingExtensions
{
    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnything<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAnything((isSatisfied, results) => [metadata(isSatisfied, results)]);

    public static IYieldFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAnything<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnyMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<bool, IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAnything((isSatisfied, _) => metadata(isSatisfied));
}