using Karlssberg.Motiv.Propositions;

namespace Karlssberg.Motiv.Tests;

public class ThrowingSpec<TModel, TMetadata>(string description, Exception exception) : SpecBase<TModel, TMetadata>
{
    public override IProposition Proposition => new Proposition(description);
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => throw exception;
}