using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
    bool isSatisfied,
    Lazy<MetadataNode<TMetadata>> metadata,
    Lazy<Explanation> explanation,
    Lazy<ResultDescriptionBase> description)
    : BooleanResultBase<TMetadata>
{
    public override MetadataNode<TMetadata> MetadataTier => metadata.Value;

    public override IEnumerable<BooleanResultBase> Underlying => [];

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        [];

    public override IEnumerable<BooleanResultBase> Causes => [];

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => [];

    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description => description.Value;

    public override Explanation Explanation => explanation.Value;
}
