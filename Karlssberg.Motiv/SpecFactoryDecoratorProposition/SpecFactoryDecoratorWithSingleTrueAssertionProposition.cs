namespace Karlssberg.Motiv.SpecFactoryDecoratorProposition;

internal sealed class SpecFactoryDecoratorWithSingleTrueAssertionProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    string whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    string? propositionalStatement = null)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => new SpecDescription(
        propositionalStatement ?? whenTrue);

    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
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