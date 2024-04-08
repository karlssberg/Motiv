namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorExplanationProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    public override IProposition Proposition => new Proposition(propositionalAssertion, UnderlyingSpec.Proposition);

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);

        var assertion = booleanResult.Satisfied switch
        {
            true => trueBecause(model, booleanResult),
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
            assertion);
    }
}