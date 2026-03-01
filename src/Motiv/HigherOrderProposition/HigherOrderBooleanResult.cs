using Motiv.Shared;

namespace Motiv.HigherOrderProposition;

internal sealed class HigherOrderBooleanResult<TMetadata, TUnderlyingMetadata>(
    bool isSatisfied,
    Lazy<IEnumerable<TMetadata>> metadata,
    Lazy<IEnumerable<string>> assertions,
    Lazy<ResultDescriptionBase> description,
    IEnumerable<BooleanResultBase<TUnderlyingMetadata>> underlyingResults,
    Lazy<IEnumerable<BooleanResultBase<TUnderlyingMetadata>>> causes)
    : BooleanResultBase<TMetadata>
{
    private readonly Lazy<MetadataNode<TMetadata>> _metadataTier = new (() =>
        new MetadataNode<TMetadata>(
            metadata.Value,
            causes.Value as IEnumerable<BooleanResultBase<TMetadata>> ?? []));

    private readonly Lazy<Explanation> _explanation =
        new (() => new Explanation(assertions.Value, causes.Value, underlyingResults));

    public override MetadataNode<TMetadata> MetadataTier => _metadataTier.Value;

    public override Explanation Explanation => _explanation.Value;
    public override IEnumerable<BooleanResultBase> Underlying => underlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues =>
        underlyingResults as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override IEnumerable<BooleanResultBase> Causes => causes.Value;

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues =>
        causes.Value as IEnumerable<BooleanResultBase<TMetadata>> ?? [];

    public override bool Satisfied { get; } = isSatisfied;

    public override ResultDescriptionBase Description => description.Value;
}
