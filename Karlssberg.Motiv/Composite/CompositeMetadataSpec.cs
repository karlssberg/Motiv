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
        
        var assertion = metadata switch {
            string because => because,
            _ => Proposition.ToReason(booleanResult.Satisfied)
        };
        
        var metadataTree = new MetadataTree<TMetadata>(
            metadata, 
            booleanResult.ResolveMetadataTrees<TMetadata, TUnderlyingMetadata>());

        return new CompositeBooleanResult<TMetadata, TUnderlyingMetadata>(
            booleanResult,
            metadataTree,
            assertion.ToEnumerable(),
            Proposition.ToReason(booleanResult.Satisfied));
    }
}