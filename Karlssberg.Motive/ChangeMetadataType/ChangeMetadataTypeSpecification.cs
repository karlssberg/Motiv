namespace Karlssberg.Motive.ChangeMetadataType;

internal class ChangeMetadataTypeSpecification<TModel, TMetadata, TOtherMetadata>(
    SpecificationBase<TModel, TOtherMetadata> underlyingSpecification,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    public SpecificationBase<TModel, TOtherMetadata> UnderlyingSpecification => underlyingSpecification;
    public override string Description => underlyingSpecification.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpecification.IsSatisfiedBy(model);
        var metadata = booleanResult.IsSatisfied
            ? whenTrue(model)
            : whenFalse(model);
        
        return new ChangeMetadataTypeBooleanResult<TMetadata, TOtherMetadata>(booleanResult, metadata);
    }
}