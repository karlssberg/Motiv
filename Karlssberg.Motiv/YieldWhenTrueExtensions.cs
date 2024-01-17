using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.SpecBuilder.Phase2;

namespace Karlssberg.Motiv;

public static class YieldWhenTrueExtensions
{
    public static IRequireFalseMetadata<TModel, TNewMetadata> YieldWhenTrue<TModel, TMetadata, TNewMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        TNewMetadata metadata) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(metadata);
    public static IRequireFalseMetadata<TModel, TNewMetadata> YieldWhenTrue<TModel, TMetadata, TNewMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TNewMetadata> metadata) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(metadata);
    public static IRequireFalseMetadata<TModel, TNewMetadata> YieldWhenTrue<TModel, TMetadata, TNewMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TModel, TNewMetadata> metadata) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(metadata);
}