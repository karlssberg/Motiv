using Humanizer;

namespace Karlssberg.Motiv.ChangeMetadataType;

internal class ChangeHigherOrderMetadataBooleanResult<TMetadata, TUnderlyingResult>(
    IEnumerable<TMetadata> metadata,
    BooleanResultBase<TUnderlyingResult> underlyingResult)
    : BooleanResultBase<TMetadata>, IChangeHigherOrderMetadataBooleanResult<TMetadata>
{
    public override bool Satisfied { get; } = underlyingResult.Satisfied;
    public BooleanResultBase<TUnderlyingResult> UnderlyingResult => underlyingResult;

    public override string Description =>
        Metadata switch
        {
            IEnumerable<Reason> reasons => reasons.Humanize(),
            _ => underlyingResult.Description
        };

    public Type OriginalMetadataType => typeof(TMetadata);

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults =>
        underlyingResult switch
        {
            BooleanResultBase<TMetadata> result => [result],
            _ => []
        };

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands =>
        underlyingResult switch
        {
            BooleanResultBase<TMetadata> result => result.DeterminativeOperands,
            _ => []
        };

    public IEnumerable<TMetadata> Metadata => metadata;

    public override IEnumerable<Reason> GatherReasons() =>
        Metadata switch
        {
            IEnumerable<string> reasons => [new Reason(reasons.Humanize(), underlyingResult.GatherReasons())],
            _ => underlyingResult.GatherReasons()
        };
}