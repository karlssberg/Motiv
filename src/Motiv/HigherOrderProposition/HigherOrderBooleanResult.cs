using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderBooleanResult<TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    Func<IEnumerable<TMetadata>> metadataFactory,
    Func<IEnumerable<string>> assertionsFactory,
    Func<ResultDescriptionBase> descriptionFactory,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> underlyingResults,
    Func<IEnumerable<BooleanResultBase<TUnderlyingMetadata>>> causesFactory)
    : BooleanResultBase<TMetadata>
{
    private IEnumerable<BooleanResultBase<TUnderlyingMetadata>>? _causes;
    private MetadataNode<TMetadata>? _metadataTier;
    private Explanation? _explanation;
    private ResultDescriptionBase? _description;

    private IEnumerable<BooleanResultBase<TUnderlyingMetadata>> CausesInternal =>
        _causes ??= causesFactory();

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier ??=
        new MetadataNode<TMetadata>(
            metadataFactory(),
            CausesInternal as IEnumerable<BooleanResultBase<TMetadata>> ?? []);

    public override Explanation Explanation => _explanation ??=
        new Explanation(assertionsFactory(), CausesInternal, underlyingResults);

    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => CausesInternal;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        CausesInternal as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description => _description ??= descriptionFactory();
}
