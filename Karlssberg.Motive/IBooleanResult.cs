namespace Karlssberg.Motive;

public interface IBooleanResult<out TMetadata>
{
    TMetadata Metadata { get; }
    bool IsSatisfied { get; }
    string Description { get; }
}