namespace Motiv.Shared;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class PolicyResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    Func<TMetadata> value,
    Func<MetadataNode<TMetadata>> metadataTier,
    Func<Explanation> explanation,
    Func<ResultDescriptionBase> description)
    : PolicyResultBase<TMetadata>
{
    public override TMetadata Value => value();

    public override bool Satisfied { get; } = booleanResult.Satisfied;


    public override ResultDescriptionBase Description => description();

    public override Explanation Explanation => explanation();

    public override MetadataNode<TMetadata> MetadataTier => metadataTier();

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();


    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.ToEnumerable();


    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];
}
