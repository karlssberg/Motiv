namespace Karlssberg.Motiv;

internal class ChangeDescriptionSpecification<TModel, TMetadata>(
    string description,
    SpecificationBase<TModel, TMetadata> underlyingSpecification) : SpecificationBase<TModel, TMetadata>
{
    public override string Description => description;

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => 
        underlyingSpecification.IsSatisfiedBy(model);
}