using Humanizer;

namespace Karlssberg.Motiv;

public sealed record Cause<TMetadata>(IEnumerable<TMetadata> Metadata, IEnumerable<string> Reasons)
{
    public Cause(TMetadata metadata, IResultDescription description) : this([metadata], [description.Reason])
    {
    }
    
    public IEnumerable<Cause<TMetadata>> Underlying { get; internal set; } = Enumerable.Empty<Cause<TMetadata>>();
    
    public IEnumerable<TMetadata> Metadata { get; } = Metadata;
    public IEnumerable<string> Reasons { get; } = Reasons;

    public override string ToString() => Reasons.Humanize();
}