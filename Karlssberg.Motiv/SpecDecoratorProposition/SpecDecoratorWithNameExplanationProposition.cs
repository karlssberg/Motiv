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

        var assertion = booleanResult.Satisfied switch
        {
            true => trueBecause,
            false => falseBecause(model, booleanResult)
        };
        
        var metadataTree = new MetadataTree<string>(
            assertion,
            booleanResult.ResolveMetadataTrees<string, TUnderlyingMetadata>());
        
        var explanation = new Explanation(assertion)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };
        
        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult, 
            metadataTree,
            explanation,
            Description.ToReason(booleanResult.Satisfied));
    }
}