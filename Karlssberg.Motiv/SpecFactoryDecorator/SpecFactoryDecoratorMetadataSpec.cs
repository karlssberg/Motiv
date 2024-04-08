namespace Karlssberg.Motiv.SpecFactoryDecorator;

internal sealed class SpecFactoryDecoratorMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var assertion = metadata switch {
            string reason => reason,
            _ => Proposition.ToReason(booleanResult.Satisfied)
        };
        
        var metadataTree = new MetadataTree<TMetadata>(
            metadata,
            booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());
        
        var explanation = new Explanation(assertion)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        return new BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadataTree,
            explanation,
            Proposition.ToReason(booleanResult.Satisfied));
    }
}