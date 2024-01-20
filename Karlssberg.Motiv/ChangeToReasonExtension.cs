using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.SpecBuilder.Phase2;

namespace Karlssberg.Motiv;

public static class ChangeToReasonExtension
{
    public static IRequireFalseReason<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        string trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(trueBecause);
    public static IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<string> trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(trueBecause);
    public static IRequireFalseReasonWhenDescriptionUnresolved<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecBase<TModel, TMetadata> spec,
        Func<TModel, string> trueBecause) =>
        new ChangeMetadataBuilder<TModel, TMetadata>(spec).YieldWhenTrue(trueBecause);
}