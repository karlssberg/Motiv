using Karlssberg.Motiv.Builder;

namespace Karlssberg.Motiv;

public static class ChangeMetadataTypeExtensions
{
    public static IRequireFalseMetadata<TModel, TNewMetadata> YieldWhenTrue<TModel, TMetadata, TNewMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TNewMetadata metadata) => 
        new ChangeMetadataBuilder<TModel, TMetadata>(specification).YieldWhenTrue(metadata);
    public static IRequireFalseReason<TModel> YieldWhenTrue<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        string trueBecause) => 
        new ChangeMetadataBuilder<TModel, TMetadata>(specification).YieldWhenTrue(trueBecause);
}