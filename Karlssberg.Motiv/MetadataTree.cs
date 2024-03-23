using System.Collections;

namespace Karlssberg.Motiv;

public sealed class MetadataTree<TMetadata>(
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<MetadataTree<TMetadata>>? underlying = null)
    : IEnumerable<TMetadata>
{
    public MetadataTree(TMetadata metadata, IEnumerable<MetadataTree<TMetadata>>? underlying = null)
        : this(metadata.ToEnumerable(), underlying)
    {
    }

    public IEnumerable<MetadataTree<TMetadata>> Underlying => underlying?? Enumerable.Empty<MetadataTree<TMetadata>>();
    
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
            IEnumerable<string> assertions => Serialize(assertions),
            IEnumerable<byte> n => Serialize(n),
            IEnumerable<sbyte> n => Serialize(n),
            IEnumerable<short> n => Serialize(n),
            IEnumerable<ushort> n => Serialize(n),
            IEnumerable<int> n => Serialize(n),
            IEnumerable<uint> n => Serialize(n),
            IEnumerable<long> n => Serialize(n),
            IEnumerable<ulong> n => Serialize(n),
            IEnumerable<float> n => Serialize(n),
            IEnumerable<double> n => Serialize(n),
            IEnumerable<char> c => Serialize(c),
            IEnumerable<decimal> n => Serialize(n),
            IEnumerable<bool> b => Serialize(b),
            IEnumerable<DateTime> dateTime => Serialize(dateTime),
            IEnumerable<TimeSpan> timeSpans => Serialize(timeSpans),
            _ => base.ToString()
        } ?? string.Empty;

    private static string Serialize<T>(IEnumerable<T> n) => string.Join(", ", n);
}