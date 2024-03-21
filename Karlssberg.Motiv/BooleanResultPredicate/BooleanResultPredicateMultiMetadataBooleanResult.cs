﻿using Karlssberg.Motiv.CompositeFactory;

namespace Karlssberg.Motiv.BooleanResultPredicate;

internal sealed class BooleanResultPredicateMultiMetadataBooleanResult<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    IEnumerable<TMetadata> metadata,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    /// <summary>Gets a value indicating whether the boolean result is satisfied.</summary>
    public override bool Satisfied => booleanResult.Satisfied;

    /// <summary>Gets the description of the boolean result.</summary>
    public override ResultDescriptionBase Description =>
        new BooleanResultDescriptionWithUnderlying<TUnderlyingMetadata>(
            booleanResult,
            proposition.ToReason(booleanResult.Satisfied));

    /// <summary>Gets the reasons for the boolean result.</summary>
    public override Explanation Explanation =>
        new(GetAssertions())
        {
            Underlying = booleanResult.Explanation.ToEnumerable()
        };

    public override MetadataTree<TMetadata> MetadataTree => new(
        metadata,
        booleanResult.ResolveMetadataSets<TMetadata, TUnderlyingMetadata>());

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata =>
        booleanResult.ResolveUnderlyingWithMetadata<TMetadata, TUnderlyingMetadata>();

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.Causes;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        booleanResult.ResolveCausesWithMetadata<TMetadata, TUnderlyingMetadata>();

    private IEnumerable<string> GetAssertions() =>
        metadata switch
        {
            IEnumerable<string> assertions => assertions,
            _ => proposition.ToReason(booleanResult.Satisfied).ToEnumerable()
        };
}