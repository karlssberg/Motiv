using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Metadata;
using Karlssberg.Motiv.HigherOrderSpecBuilder.YieldWhenTrue.Reasons;

namespace Karlssberg.Motiv;

public static class YieldWhenAllTrueExtensions
{
    public static IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, TMetadata> metadata) =>
        builder.YieldWhenAllTrue(results => [metadata(results)]);

    public static IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        Func<IEnumerable<TMetadata>> metadata) =>
        builder.YieldWhenAllTrue(_ => metadata());

    public static IYieldAnyTrueMetadataOrFalseMetadata<TModel, TMetadata, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TMetadata, TUnderlyingMetadata>(
        this IYieldAllTrueMetadata<TModel, TMetadata, TUnderlyingMetadata> builder,
        TMetadata metadata) =>
        builder.YieldWhenAllTrue(() => [metadata]);
    
    
    
    public static IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TUnderlyingMetadata>(
        this IYieldAllTrueReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<BooleanResultWithModel<TModel, TUnderlyingMetadata>>, string> trueBecause) =>
        builder.YieldWhenAllTrue(results => [trueBecause(results)]);
    
    public static IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TUnderlyingMetadata>(
        this IYieldAllTrueReasons<TModel, TUnderlyingMetadata> builder,
        Func<IEnumerable<string>> trueBecause) =>
        builder.YieldWhenAllTrue(_ => trueBecause());
    
    public static IYieldAnyTrueReasonsOrFalseReasons<TModel, TUnderlyingMetadata> YieldWhenAllTrue<TModel, TUnderlyingMetadata>(
        this IYieldAllTrueReasons<TModel, TUnderlyingMetadata> builder,
        string trueBecause) =>
        builder.YieldWhenAllTrue(_ => trueBecause);
}