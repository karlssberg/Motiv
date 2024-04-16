namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorWithNameExplanationProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => new SpecDescription(propositionalAssertion, underlyingSpec.Description.Detailed);

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);

        var assertion = new Lazy<string>(() => booleanResult.Satisfied switch
        {
            true => trueBecause,
            false => falseBecause(model, booleanResult)
        });

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult, 
            MetadataTree,
            Explanation,
            Description.ToReason(booleanResult.Satisfied));

        Explanation Explanation() => new(assertion.Value)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        MetadataTree<string> MetadataTree() =>
            new(assertion.Value, 
                booleanResult.ResolveMetadataTrees<string, TUnderlyingMetadata>());
    }
}