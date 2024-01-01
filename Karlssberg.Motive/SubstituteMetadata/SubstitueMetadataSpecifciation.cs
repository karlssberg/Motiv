namespace Karlssberg.Motive.SubstituteMetadata;

internal class SubstituteMetadataSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<TModel, TMetadata> whenTrue,
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    public override string Description => underlyingSpecification.Description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecification.IsSatisfiedBy(model);
        return booleanResult.IsSatisfied
            ? new SubstituteMetadataBooleanResult<TMetadata>(booleanResult, whenTrue(model))
            : new SubstituteMetadataBooleanResult<TMetadata>(booleanResult, whenFalse(model));
    }
}