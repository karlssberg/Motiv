using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

namespace Karlssberg.Motiv;

public static class YieldExtensions
{
    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata> builder,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.Yield<TMetadata>((isSatisfied, results) => [metadata(isSatisfied, results)]);

    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata> builder,
        Func<bool, TMetadata> metadata) =>
        builder.Yield<TMetadata>((isSatisfied, _) => [metadata(isSatisfied)]);

    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.Yield((isSatisfied, results) => [metadata(isSatisfied, results)]);
}