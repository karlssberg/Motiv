using Karlssberg.Motiv.HigherOrderSpecBuilder;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;

namespace Karlssberg.Motiv;

public static class YieldAnythingTypeConverterExtensions
{
    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata> converter,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        converter.Yield<TMetadata>((isSatisfied, results) => [metadata(isSatisfied, results)]);

    public static IHigherOrderSpecFactory<TModel, TMetadata> Yield<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAnythingTypeConverter<TModel, TUnderlyingMetadata> converter,
        Func<bool, TMetadata> metadata) =>
        converter.Yield<TMetadata>((isSatisfied, _) => [metadata(isSatisfied)]);
}