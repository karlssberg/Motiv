using Karlssberg.Motiv.CollectionBuilder;

namespace Karlssberg.Motiv;

public static class CollectionWhenTrueSpecBuilderExtensions
{
    public static YieldTrueOrFalseBuilder<TModel, TMetadata> YieldWhenAnyTrue<TModel, TMetadata>(
        this IYieldMetadata<TModel, TMetadata> builder,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenTrue) =>
        new(builder, whenTrue);

    public static YieldTrueOrFalseBuilder<TModel, TMetadata> YieldWhenAnyTrue<TModel, TMetadata>(
        this IYieldMetadata<TModel, TMetadata> builder,
        Func<TMetadata> whenTrue) =>
        new(builder, _ => whenTrue());

    public static YieldTrueOrFalseBuilder<TModel, TMetadata> YieldWhenAnyTrue<TModel, TMetadata>(
        this IYieldMetadata<TModel, TMetadata> builder,
        TMetadata whenTrue) =>
        new(builder, _ => whenTrue);
}