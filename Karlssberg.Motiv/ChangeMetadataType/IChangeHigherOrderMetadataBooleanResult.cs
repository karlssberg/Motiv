namespace Karlssberg.Motiv.ChangeMetadataType;

public interface IChangeHigherOrderMetadataBooleanResult<TMetadata>
{
    bool Satisfied { get; }
    string Description { get; }
    Type OriginalMetadataType { get; }
    IEnumerable<BooleanResultBase<TMetadata>> UnderlyingResults { get; }
    IEnumerable<BooleanResultBase<TMetadata>> Causes { get; }
    IEnumerable<TMetadata> Metadata { get; }

    /// <summary>Gets the unique specific underlying reasons why the condition is satisfied or not.</summary>
    IEnumerable<Reason> ReasonHierarchy { get; }
}