namespace Motiv;

/// <summary>Represents a node in the metadata hierarchy.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class MetadataNode<TMetadata>
{
    private readonly Lazy<ISet<TMetadata>> _lazyMetadataCollection;

    private readonly Lazy<IEnumerable<MetadataNode<TMetadata>>> _lazyUnderlying;

    /// <summary>Initializes a new instance of the MetadataNode class.</summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    /// <param name="causes">The causes of the metadata.</param>
    public MetadataNode(
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        var lazyMetadata = new Lazy<IEnumerable<TMetadata>>(() =>
            metadata as ICollection<TMetadata> ?? metadata.ToArray());

        _lazyUnderlying = new Lazy<IEnumerable<MetadataNode<TMetadata>>>(() =>
            ResolveUnderlying(lazyMetadata.Value, causes));

        _lazyMetadataCollection = new Lazy<ISet<TMetadata>>(() =>
            metadata switch
            {
                ISet<TMetadata> metadataTier => metadataTier,
                IEnumerable<IComparable<TMetadata>> => new SortedSet<TMetadata>(lazyMetadata.Value),
                _ => new HashSet<TMetadata>(lazyMetadata.Value)
            });
    }

    /// <summary>Initializes a new instance of the MetadataNode class with a single metadata item.</summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    /// <param name="causes">The causes of the metadata.</param>
    public MetadataNode(TMetadata metadata, IEnumerable<BooleanResultBase<TMetadata>> causes)
        : this(metadata.ToEnumerable(), causes)
    {
    }

    /// <summary>Gets the underlying metadata nodes.</summary>
    public IEnumerable<MetadataNode<TMetadata>> Underlying => _lazyUnderlying.Value;

    /// <summary>Gets the metadata associated with this node.</summary>
    public IEnumerable<TMetadata> Metadata => _lazyMetadataCollection.Value;

    /// <summary>Returns a string that represents the current object.</summary>
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
            .DistinctWithOrderPreserved();

        var doesParentEqualChildAssertion = underlyingMetadata.SequenceEqual(metadata);

        return doesParentEqualChildAssertion
            ? underlying.SelectMany(result => result.Underlying)
            : underlying;
    }

    private string GetDebugDisplay()
    {
        return _lazyMetadataCollection.Value switch
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
            _ when typeof(TMetadata).IsEnum => _lazyMetadataCollection.Value.Serialize(),
            _ => base.ToString() ?? ""
        };
    }
}
