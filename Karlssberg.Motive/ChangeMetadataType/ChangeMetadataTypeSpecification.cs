namespace Karlssberg.Motive.ChangeMetadataType;

internal class ChangeMetadataTypeSpecification<TModel, TMetadata, TOtherMetadata>(
    string description,
    SpecificationBase<TModel, TOtherMetadata> underlyingSpecification,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    internal ChangeMetadataTypeSpecification(
        SpecificationBase<TModel, TOtherMetadata> underlyingSpecification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse)
        : this(
            underlyingSpecification.Description,
            underlyingSpecification,
            whenTrue,
            whenFalse)
    {
    }
    
    public override string Description => description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecification.IsSatisfiedBy(model);
        var metadata = booleanResult.IsSatisfied
            ? whenTrue(model)
            : whenFalse(model);
        
        return new ChangeMetadataTypeBooleanResult<TMetadata, TOtherMetadata>(booleanResult, metadata);
    }
}