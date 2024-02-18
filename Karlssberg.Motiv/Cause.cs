using System.Collections;
using Humanizer;

namespace Karlssberg.Motiv;

public record Cause<TMetadata>(IEnumerable<TMetadata> Metadata, IEnumerable<string> Reasons)
{
    public Cause(TMetadata metadata, string reason) : this([metadata], [reason])
    {
    }
    
    public IEnumerable<Cause<TMetadata>> Underlying { get; internal set; } = Enumerable.Empty<Cause<TMetadata>>();
    
    public IEnumerable<TMetadata> Metadata { get; } = Metadata;
    public IEnumerable<string> Reasons { get; } = Reasons;

    public override string ToString() => Reasons.Humanize();
}

public class MetadataSet<TMetadata>(IEnumerable<TMetadata> metadataCollection) : IEnumerable<TMetadata>
{
    public MetadataSet(TMetadata metadata) : this([metadata])
    {
    }
    
    private readonly ISet  <TMetadata> _metadataCollection = metadataCollection switch
    {
        IEnumerable<IComparable<TMetadata>> => new SortedSet<TMetadata>(metadataCollection),
        _ => new HashSet<TMetadata>(metadataCollection)
    };

    public IEnumerator<TMetadata> GetEnumerator() => _metadataCollection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_metadataCollection).GetEnumerator();

    public override string ToString() =>
        _metadataCollection switch
        {
            IEnumerable<string> reasons => reasons.Humanize(),
            _ when typeof(TMetadata).IsPrimitive => _metadataCollection.Humanize(),
            _ => base.ToString()
        };
}
