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
    private readonly BooleanResultBase<TUnderlyingMetadata>[] _underlyingResults = [booleanResult];
    private MetadataNode<TMetadata>? _metadataTier;
    private Explanation? _explanation;
    private ResultDescriptionBase? _description;

    public override bool Satisfied { get; } = booleanResult.Satisfied;

    public override ResultDescriptionBase Description => _description ??= descriptionFactory();

    public override Explanation Explanation => _explanation ??= explanationFactory();

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier ??= metadataTierFactory();

    public override IEnumerable<BooleanResultBase> Underlying => _underlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => _underlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        _underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];
}
