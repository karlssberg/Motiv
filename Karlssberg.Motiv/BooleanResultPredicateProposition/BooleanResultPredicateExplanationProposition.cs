﻿namespace Karlssberg.Motiv.BooleanResultPredicateProposition;

internal sealed class BooleanResultPredicateExplanationProposition<TModel, TUnderlyingMetadata>(
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> whenFalse,
    ISpecDescription specDescription)
    : SpecBase<TModel, string>
{
    public override ISpecDescription Description => specDescription;
    
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = predicate(model);
        
        var assertion = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var metadataTree = new MetadataTree<string>(
            assertion.ToEnumerable(),
            booleanResult.ResolveMetadataTrees<string, TUnderlyingMetadata>());
        
        var explanation = new Explanation(assertion)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        return new BooleanResultWithUnderlying<string, TUnderlyingMetadata>(
            booleanResult,
            metadataTree,
            explanation,
            specDescription.ToReason(booleanResult.Satisfied));
    }
}