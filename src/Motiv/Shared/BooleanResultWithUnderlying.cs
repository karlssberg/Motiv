namespace Motiv.Shared;

/// <summary>Represents a boolean result of changing the metadata type.</summary>
/// <typeparam name="TMetadata">The type of the new metadata.</typeparam>
/// <typeparam name="TUnderlyingMetadata">The type of the original metadata.</typeparam>
internal sealed class BooleanResultWithUnderlying<TMetadata, TUnderlyingMetadata>(
    BooleanResultBase<TUnderlyingMetadata> booleanResult,
    Func<MetadataNode<TMetadata>> metadataTierFactory,
    Func<Explanation> explanationFactory,
    Func<ResultDescriptionBase> descriptionFactory)
    : BooleanResultBase<TMetadata>
{
    private MetadataNode<TMetadata>? _metadataTier;
    private Explanation? _explanation;
    private ResultDescriptionBase? _description;

    public override bool Satisfied { get; } = booleanResult.Satisfied;

    public override ResultDescriptionBase Description => _description ??= descriptionFactory();

    public override Explanation Explanation => _explanation ??= explanationFactory();

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier ??= metadataTierFactory();

    public override IEnumerable<BooleanResultBase> Underlying => booleanResult.ToEnumerable();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => booleanResult.ToEnumerable();


    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        booleanResult.ToEnumerable() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];
}
