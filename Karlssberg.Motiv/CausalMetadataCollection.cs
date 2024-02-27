using System.Collections;
using Humanizer;

namespace Karlssberg.Motiv;

public sealed class  CausalMetadataCollection<TMetadata>(
    IEnumerable<TMetadata> metadata,
    IEnumerable<string> explanations)
    : IEnumerable<TMetadata>
{
    public CausalMetadataCollection(TMetadata metadata, ResultDescriptionBase description) : this(metadata.ToEnumerable(), description.Compact.ToEnumerable())
    {
    }
    
    public IEnumerable<CausalMetadataCollection<TMetadata>> Underlying { get; internal set; } = Enumerable.Empty<CausalMetadataCollection<TMetadata>>();
    
    public IEnumerable<TMetadata> Metadata => metadata;

    public IEnumerator<TMetadata> GetEnumerator() => Metadata.GetEnumerator();

    public override string ToString() => explanations.Humanize();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}