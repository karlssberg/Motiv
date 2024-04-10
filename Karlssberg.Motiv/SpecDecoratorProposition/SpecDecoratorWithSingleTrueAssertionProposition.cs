namespace Karlssberg.Motiv.SpecDecoratorProposition;

internal sealed class SpecDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    string whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    string? propositionalStatement = null)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => new SpecDescription(
        propositionalStatement ?? whenTrue,
        underlyingSpec.Description.Detailed);

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpec.IsSatisfiedBy(model);
        
        var assertion = booleanResult.Satisfied switch
        {
            true => whenTrue,
            false => whenFalse(model, booleanResult)
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
            propositionalStatement is null
                ? assertion
                : propositionalStatement.ToReason(booleanResult.Satisfied));
    }
}