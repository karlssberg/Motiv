namespace Karlssberg.Motive.ChangeMetadataType;

public interface IChangeMetadataTypeBooleanResult<out TMetadata>
{
    TMetadata Metadata { get; }
    bool IsSatisfied { get; }
    string Description { get; }
}