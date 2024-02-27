using System.Collections;
using Humanizer;

namespace Karlssberg.Motiv;

public sealed class MetadataSet<TMetadata>(
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<MetadataSet<TMetadata>>? underlying = null) : IEnumerable<TMetadata>
{
    public MetadataSet(TMetadata metadata) : this(metadata.ToEnumerable())
    {
    }
    
    public IEnumerable<MetadataSet<TMetadata>> Underlying { get; } = 
        underlying ?? Enumerable.Empty<MetadataSet<TMetadata>>();
    
    private readonly ISet<TMetadata> _metadataCollection = metadataCollection switch
    {
        ISet<TMetadata> metadataSet => metadataSet,
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