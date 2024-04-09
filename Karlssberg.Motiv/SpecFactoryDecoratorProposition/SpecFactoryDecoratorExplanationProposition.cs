﻿namespace Karlssberg.Motiv.SpecFactoryDecoratorProposition;

internal sealed class SpecFactoryDecoratorExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    
    public override ISpecDescription Description => new SpecDescription(propositionalAssertion);

    
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var assertion = booleanResult.Satisfied switch
        {
            true => trueBecause(model, booleanResult),
            false => falseBecause(model, booleanResult),
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