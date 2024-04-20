namespace Karlssberg.Motiv;

public sealed class MetadataNode<TMetadata>(
    IEnumerable<TMetadata> metadataCollection,
    IEnumerable<MetadataNode<TMetadata>> underlying)
{
    public MetadataNode(IEnumerable<TMetadata> metadataCollection)
        : this(metadataCollection, Enumerable.Empty<MetadataNode<TMetadata>>())
    {
    }
    
    public MetadataNode(TMetadata metadata)
        : this(metadata.ToEnumerable(), Enumerable.Empty<MetadataNode<TMetadata>>())
    {
    }
    
    public MetadataNode(TMetadata metadata, IEnumerable<MetadataNode<TMetadata>> underlying)
        : this(metadata.ToEnumerable(), underlying)
    {
    }
    
    public IEnumerable<MetadataNode<TMetadata>> Underlying => underlying;
    
    public IEnumerable<TMetadata> Metadata => _metadataCollection;
    
    public int Count => _metadataCollection.Count;

    private readonly ISet<TMetadata> _metadataCollection = metadataCollection switch
    {
        ISet<TMetadata> metadataTier => metadataTier,
        IEnumerable<IComparable<TMetadata>> => new SortedSet<TMetadata>(metadataCollection),
        _ => new HashSet<TMetadata>(metadataCollection)
    };

    public override string ToString() => GetDebugDisplay();
    
    private string GetDebugDisplay()
    {
        return _metadataCollection switch
        {
            IEnumerable<string> assertions => assertions.Serialize(),
            IEnumerable<byte> numerics => numerics.Serialize(),
            IEnumerable<sbyte> numerics => numerics.Serialize(),
            IEnumerable<short> numerics => numerics.Serialize(),
            IEnumerable<ushort> numerics => numerics.Serialize(),
            IEnumerable<int> numerics => numerics.Serialize(),
            IEnumerable<uint> numerics => numerics.Serialize(),
            IEnumerable<long> numerics => numerics.Serialize(),
            IEnumerable<ulong> numerics => numerics.Serialize(),
            IEnumerable<float> numerics => numerics.Serialize(),
            IEnumerable<double> numerics => numerics.Serialize(),
            IEnumerable<char> characters => characters.Serialize(),
            IEnumerable<decimal> numerics => numerics.Serialize(),
            IEnumerable<bool> booleans => booleans.Serialize(),
            IEnumerable<DateTime> dateTimes => dateTimes.Serialize(),
            IEnumerable<TimeSpan> timeSpans => timeSpans.Serialize(),
            _ when typeof(TMetadata).IsEnum => _metadataCollection.Serialize(),
            _ => base.ToString() ?? ""
        };
    }
}