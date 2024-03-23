﻿namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    /// <inheritdoc />
    public override IProposition Proposition => new Proposition(propositionalAssertion, UnderlyingSpec.Proposition);

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    /// <inheritdoc />
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };
        
        var because = metadata switch {
            string reason => reason,
            _ => Proposition.ToReason(booleanResult.Satisfied)
        };

        return new CompositeBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata, because);
    }
}