using System.Collections;

namespace Karlssberg.Motiv;

public sealed class MetadataTree<TMetadata>(
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<MetadataTree<TMetadata>>? underlying = null) : IEnumerable<TMetadata>
{
    public MetadataTree(TMetadata metadata, IEnumerable<MetadataTree<TMetadata>>? underlying = null)
        : this(metadata.ToEnumerable(), underlying)
    {
    }

    public IEnumerable<MetadataTree<TMetadata>> Underlying { get; } =
        underlying ?? Enumerable.Empty<MetadataTree<TMetadata>>();
    
    public int Count => _metadataCollection.Count;

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
            IEnumerable<string> reasons => string.Join(", ", reasons),
            _ when typeof(TMetadata).IsPrimitive => SerializeMetadata(),
            _ => base.ToString()!
        };

    private string SerializeMetadata()
    {
        return string.Join(", ", _metadataCollection
            .Select(m => m?.ToString())
            .Where(m => !string.IsNullOrWhiteSpace(m)));
    }
}