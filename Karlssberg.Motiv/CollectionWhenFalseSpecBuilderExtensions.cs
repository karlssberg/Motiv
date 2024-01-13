using Karlssberg.Motiv.CollectionBuilder;

namespace Karlssberg.Motiv;

public static class CollectionWhenFalseSpecBuilderExtensions
{
    public static IYieldFalseMetadata<TModel, TMetadata> YieldWhenAnyFalse<TModel, TMetadata>(
        this YieldTrueOrFalseBuilder<TModel, TMetadata> builder,
        Func<TMetadata> whenFalse) =>
        builder.YieldWhenAnyFalse(_ => whenFalse());
    
    public static IYieldFalseMetadata<TModel, TMetadata> YieldWhenAnyFalse<TModel, TMetadata>(
        this YieldTrueOrFalseBuilder<TModel, TMetadata> builder,
        TMetadata whenFalse) =>
        builder.YieldWhenAnyFalse(_ => whenFalse);
     
    public static IYieldFalseMetadata<TModel, TMetadata> YieldWhenAny<TModel, TMetadata>(
        this IYieldMetadata<TModel, TMetadata> builder,
        Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>,TMetadata> whenAny) =>
        builder.YieldWhenAny((isSatisfied, results) => [whenAny(isSatisfied, results)]);
}