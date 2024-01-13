namespace Karlssberg.Motiv.Tests;

public class ThrowingSpec<TModel, TMetadata>(string description, Exception exception) : SpecBase<TModel, TMetadata>
{
    public override string Description => description;
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => throw exception;
}