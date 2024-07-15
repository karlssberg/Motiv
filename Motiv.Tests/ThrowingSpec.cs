namespace Motiv.Tests;

public class ThrowingSpec<TModel, TMetadata>(string description, Exception exception) : SpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => Enumerable.Empty<SpecBase>();
    public override ISpecDescription Description => new SpecDescription(description);
    internal override BooleanResultBase<TMetadata> IsSatisfiedByInternal(TModel model) => throw exception;
}
