namespace Karlssberg.Motiv;

public sealed class MetadataNode<TMetadata>
{
    public IEnumerable<MetadataNode<TMetadata>> Underlying { get; }

    public IEnumerable<TMetadata> Metadata => _metadataCollection;

    public int Count => _metadataCollection.Count;

    private readonly ISet<TMetadata> _metadataCollection;

    public MetadataNode(IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        var metadataCollection = metadata as ICollection<TMetadata> ?? metadata.ToArray();
        
        Underlying = ResolveUnderlying(metadataCollection, causes);
        _metadataCollection = metadata switch
        {
            ISet<TMetadata> metadataTier => metadataTier,
            IEnumerable<IComparable<TMetadata>> => new SortedSet<TMetadata>(metadataCollection),
            _ => new HashSet<TMetadata>(metadataCollection)
        };
    }

    private IEnumerable<MetadataNode<TMetadata>> ResolveUnderlying(
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        var causesArray = causes as BooleanResultBase<TMetadata>[] ?? causes.ToArray();

        var underlying = causesArray
            .SelectMany(cause =>
                cause switch
                {
                    IBooleanOperationResult<TMetadata> => cause.UnderlyingMetadataSources,
                    _ => cause.ToEnumerable()
                })
            .Select(cause => cause.MetadataTier)
            .ToArray();

        var underlyingMetadata = underlying
            .SelectMany(metadataNode => metadataNode.Metadata)
            .Distinct();

        var doesParentEqualChildAssertion = underlyingMetadata.SequenceEqual(metadata);

        return doesParentEqualChildAssertion
            ? underlying.SelectMany(result => result.Underlying)
            : underlying;
    }

    public MetadataNode(TMetadata metadata, IEnumerable<BooleanResultBase<TMetadata>> causes)
        : this(metadata.ToEnumerable(), causes)
    {
    }

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