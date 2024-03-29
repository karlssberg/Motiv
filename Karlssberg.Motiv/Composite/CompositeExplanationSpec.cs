﻿namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeExplanationSpec<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> trueBecause,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, string> falseBecause,
    string propositionalAssertion)
    : SpecBase<TModel, string>
{
    /// <inheritdoc />
    public override IProposition Proposition => new Proposition(propositionalAssertion, UnderlyingSpec.Proposition);

    /// <inheritdoc />
    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    /// <inheritdoc />
    public override BooleanResultBase<string> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);

        var assertion = booleanResult.Satisfied switch
        {
            true => trueBecause(model, booleanResult),
            false => falseBecause(model, booleanResult)
        };
        
        return new CompositeBooleanResult<string, TUnderlyingMetadata>(
            booleanResult, 
            assertion.ToEnumerable(),
            assertion.ToEnumerable(),
            Proposition.ToReason(booleanResult.Satisfied));
    }
}