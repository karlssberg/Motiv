namespace Karlssberg.Motiv.ChangeMetadataType;

public interface IChangeMetadataTypeBooleanResult<out TMetadata>
{
    TMetadata Metadata { get; }
    bool IsSatisfied { get; }
    string Description { get; }
    
    Type OriginalMetadataType { get; }
}