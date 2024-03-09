﻿namespace Karlssberg.Motiv.FirstOrder;

/// <summary>Represents a boolean result with associated metadata and description.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the result.</typeparam>
public sealed class BooleanResult<TMetadata>(
    bool value,
    TMetadata metadata,
    IProposition proposition)
    : BooleanResultBase<TMetadata>
{
    public override MetadataTree<TMetadata> MetadataTree => new(metadata.ToEnumerable());
    public override IEnumerable<BooleanResultBase> Underlying =>
        Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithMetadata => 
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    public override IEnumerable<BooleanResultBase> Causes =>
        Enumerable.Empty<BooleanResultBase>();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithMetadata =>
        Enumerable.Empty<BooleanResultBase<TMetadata>>();

    /// <summary>Gets the reasons for the result.</summary>
    public override Explanation Explanation => new(Description)
    {
        Underlying = Enumerable.Empty<Explanation>()
    };

    /// <summary>Gets a value indicating whether the result is satisfied.</summary>
    public override bool Satisfied => value;

    /// <summary>Gets the description of the result.</summary>
    public override ResultDescriptionBase Description =>
        new ResultDescription<TMetadata>(value, proposition, metadata);
}