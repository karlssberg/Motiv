namespace Motiv.Shared;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    Lazy<MetadataNode<TMetadata>> metadataTier,
    Lazy<Explanation> explanation,
    Lazy<ResultDescriptionBase> description)
    : BooleanResultBase<TMetadata>
{
    public override bool Satisfied { get; } = booleanResult.Satisfied;

    public override ResultDescriptionBase Description => description.Value;

    public override Explanation Explanation => explanation.Value;

    public override MetadataNode<TMetadata> MetadataTier => metadataTier.Value;

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.ToEnumerable();


    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];
}
