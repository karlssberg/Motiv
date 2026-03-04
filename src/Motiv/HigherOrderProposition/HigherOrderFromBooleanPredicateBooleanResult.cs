using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderFromBooleanPredicateBooleanResult<TMetadata>(
    bool isSatisfied,
    Func<MetadataNode<TMetadata>> metadataFactory,
    Func<Explanation> explanationFactory,
    Func<ResultDescriptionBase> descriptionFactory)
    : BooleanResultBase<TMetadata>
{
    private MetadataNode<TMetadata>? _metadataTier;
    private Explanation? _explanation;
    private ResultDescriptionBase? _description;

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier ??= metadataFactory();

    public override IEnumerable<BooleanResultBase> Underlying => [];

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        [];

    public override IEnumerable<BooleanResultBase> Causes => [];

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => [];

    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description => _description ??= descriptionFactory();

    public override Explanation Explanation => _explanation ??= explanationFactory();
}
