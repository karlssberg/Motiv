﻿namespace Karlssberg.Motiv.Composite;

internal sealed class CompositeMetadataSpec<TModel, TMetadata, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> underlyingSpec,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenTrue,
    Func<TModel, BooleanResultBase<TUnderlyingMetadata>, TMetadata> whenFalse,
    string propositionalAssertion)
    : SpecBase<TModel, TMetadata>
{
    /// <summary>Gets the description of the specification.</summary>
    public override IProposition Proposition => new Proposition(propositionalAssertion, UnderlyingSpec.Proposition);

    public SpecBase<TModel, TUnderlyingMetadata> UnderlyingSpec { get; } = underlyingSpec;

    /// <summary>Determines if the specification is satisfied by the given model.</summary>
    /// <param name="model">The model to be evaluated.</param>
    /// <returns>
    ///     A <see cref="BooleanResultBase{TMetadata}" /> indicating if the specification is satisfied and the resulting
    ///     metadata.
    /// </returns>
    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var booleanResult = UnderlyingSpec.IsSatisfiedBy(model);
        
        var metadata = booleanResult.Satisfied switch
        {
            true => whenTrue(model, booleanResult),
            false => whenFalse(model, booleanResult),
        };

        return new CompositeMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(booleanResult, metadata, Proposition);
    }
}