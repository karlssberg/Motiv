namespace Karlssberg.Motiv.SubstituteMetadata;

internal class SubstituteMetadataSpecification<TModel, TMetadata>(
    string description,
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    internal SubstituteMetadataSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<TModel, TMetadata> whenTrue,
        Func<TModel, TMetadata> whenFalse) : this(
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
        return booleanResult.IsSatisfied
            ? new SubstituteMetadataBooleanResult<TMetadata>(booleanResult, whenTrue(model))
            : new SubstituteMetadataBooleanResult<TMetadata>(booleanResult, whenFalse(model));
    }
}