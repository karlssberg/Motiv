namespace Karlssberg.Motiv;

public interface IPropositionResult<out TMetadata>
{
    bool IsSatisfied { get; }

    string Description { get; }

    TMetadata Metadata { get; }

    IEnumerable<string> Reasons { get; }
}