﻿namespace Karlssberg.Motiv.CompositeFactory;

internal sealed class CompositeFactoryExplanationSpec<TModel, TUnderlyingMetadata>(
    Func<TModel, SpecBase<TModel, TUnderlyingMetadata>> underlyingSpecFactory,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    
    public override IProposition Proposition => new Proposition(propositionalAssertion);

    
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = underlyingSpecFactory(model).IsSatisfiedBy(model);
        
        var assertion = booleanResult.Satisfied switch
        {
            true => trueBecause(model, booleanResult),
            false => falseBecause(model, booleanResult),
        };
        
        var explanation = new Explanation(assertion)
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

        return new CompositeFactoryBooleanResult<string, TUnderlyingMetadata>(
            booleanResult, 
            assertion.ToEnumerable(), 
            explanation,
            Proposition.ToReason(booleanResult.Satisfied));
    }
}