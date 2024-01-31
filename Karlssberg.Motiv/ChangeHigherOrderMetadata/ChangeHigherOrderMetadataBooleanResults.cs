using Karlssberg.Motiv.ChangeMetadata;

namespace Karlssberg.Motiv.ChangeHigherOrderMetadata;

public class ChangeHigherOrderMetadataBooleanResults<TMetadata>(
    IEnumerable<TMetadata> metadata,
    BooleanResultBase<TMetadata> underlyingResult)
    : BooleanResultBase<TMetadata>, IChangeMetadataBooleanResult<TMetadata>
{
    public override bool IsSatisfied { get; } = underlyingResult.IsSatisfied;
    
    public override string Description => UnderlyingResult.Description;

    public Type OriginalMetadataType { get; } = typeof(TMetadata);

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; } =
        underlyingResult.UnderlyingResults;

    public override IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; } =
        underlyingResult.DeterminativeOperands;

    public override IEnumerable<string> GatherReasons() => UnderlyingResult.GatherReasons();

    public IEnumerable<TMetadata> Metadata { get; } = metadata;
    
    public BooleanResultBase<TMetadata> UnderlyingResult { get; } = underlyingResult;
}