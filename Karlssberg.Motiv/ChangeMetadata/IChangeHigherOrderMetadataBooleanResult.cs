namespace Karlssberg.Motiv.ChangeMetadata;

public interface IChangeHigherOrderMetadataBooleanResult<TMetadata>
{
    bool IsSatisfied { get; }
    string Description { get; }
    Type OriginalMetadataType { get; }
    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }
    IEnumerable<BooleanResultBase<TMetadata>> DeterminativeOperands { get; }
    IEnumerable<TMetadata> Metadata { get; }

    /// <summary>Gets the unique specific underlying reasons why the condition is satisfied or not.</summary>
    IEnumerable<string> Reasons { get; }
}