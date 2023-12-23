namespace Karlssberg.Motive.Tests;

public class ThrowingSpecification<TModel, TMetadata>(string description, Exception exception) : SpecificationBase<TModel, TMetadata>
{
    public override string Description => description;
    public override BooleanResultBase<TMetadata> Evaluate(TModel model) => throw exception;
}