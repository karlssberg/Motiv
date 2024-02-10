namespace Karlssberg.Motiv;

public interface IPropositionResult<out TMetadata>
{
    bool Satisfied { get; }

    string Description { get; }

    TMetadata Metadata { get; }

    IEnumerable<Reason> Reasons { get; }
}