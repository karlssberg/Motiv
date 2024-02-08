using Humanizer;

namespace Karlssberg.Motiv.ChangeMetadataType;

public class ChangeHigherOrderMetadataBooleanResult<TMetadata, TUnderlyingResult>(
    IEnumerable<TMetadata> metadata,
    BooleanResultBase<TUnderlyingResult> underlyingResult)
    : BooleanResultBase<TMetadata>, IChangeHigherOrderMetadataBooleanResult<TMetadata>
{
    public override bool Value { get; } = underlyingResult.Value;

    public override string Description =>
        Metadata switch
        {
            IEnumerable<string> reasons => reasons.Humanize(),
            _ => underlyingResult.Description
        };

    public Type OriginalMetadataType { get; } = typeof(TMetadata);

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } = underlyingResult switch
        {
            BooleanResultBase<TMetadata> result => [result],
            _ => []
        };

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; } = underlyingResult switch
        {
            BooleanResultBase<TMetadata> result => result.DeterminativeOperands,
            _ => []
        };

    public override IEnumerable<string> GatherReasons() => Metadata switch
    {
        IEnumerable<string> reasons => reasons,
        _ => underlyingResult.GatherReasons()
    };

    public IEnumerable<TMetadata> Metadata { get; } = metadata;
    
    public BooleanResultBase<TUnderlyingResult> UnderlyingResult { get; } = underlyingResult;
}