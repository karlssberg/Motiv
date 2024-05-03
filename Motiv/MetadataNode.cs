namespace Motiv;

/// <summary>
/// Represents a node in the metadata hierarchy.
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public sealed class MetadataNode<TMetadata>
{
    /// <summary>
    /// Gets the underlying metadata nodes.
    /// </summary>
    public IEnumerable<MetadataNode<TMetadata>> Underlying { get; }

    /// <summary>
    /// Gets the metadata associated with this node.
    /// </summary>
    public IEnumerable<TMetadata> Metadata => _metadataCollection;

    /// <summary>
    /// Gets the count of metadata items in this node.
    /// </summary>
    public int Count => _metadataCollection.Count;

    private readonly ISet<TMetadata> _metadataCollection;
    
    /// <summary>
    /// Initializes a new instance of the MetadataNode class.
    /// </summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    /// <param name="causes">The causes of the metadata.</param>
    public MetadataNode(
        IEnumerable<TMetadata> metadata,
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

    /// <summary>
    /// Initializes a new instance of the MetadataNode class with a single metadata item.
    /// </summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    /// <param name="causes">The causes of the metadata.</param>
    public MetadataNode(TMetadata metadata, IEnumerable<BooleanResultBase<TMetadata>> causes)
        : this(metadata.ToEnumerable(), causes)
    {
    }
    
    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => GetDebugDisplay();

    private static IEnumerable<MetadataNode<TMetadata>> ResolveUnderlying(
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