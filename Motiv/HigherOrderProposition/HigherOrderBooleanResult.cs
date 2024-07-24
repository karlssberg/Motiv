namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderBooleanResult<TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    Func<IEnumerable<TMetadata>> metadataFn,
    Func<IEnumerable<string>> assertionsFn,
    Func<ResultDescriptionBase> descriptionFn,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> underlyingResults,
    Func<IEnumerable<BooleanResultBase<TUnderlyingMetadata>>> causesFn)
    : BooleanResultBase<TMetadata>
{
    private readonly Lazy<MetadataNode<TMetadata>> _metadataTier = new (() =>
        new MetadataNode<TMetadata>(
            metadataFn(),
            causesFn() as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

    private readonly Lazy<Explanation> _explanation =
        new (() => new Explanation(assertionsFn(), causesFn(), underlyingResults));

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier.Value;

    public override Explanation Explanation => _explanation.Value;
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => causesFn();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        causesFn() as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description => descriptionFn();
}
