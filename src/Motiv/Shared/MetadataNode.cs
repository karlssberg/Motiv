using Motiv.Traversal;

namespace Motiv.Shared;

/// <summary>Represents a node in the metadata hierarchy.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class MetadataNode<TMetadata>
{
    private static readonly IEnumerable<MetadataNode<TMetadata>> EmptyUnderlying =
        Array.Empty<MetadataNode<TMetadata>>();

    private readonly IEnumerable<TMetadata>? _metadataSource;
    private readonly IEnumerable<BooleanResultBase<TMetadata>>? _causes;
    private IEnumerable<MetadataNode<TMetadata>>? _underlying;
    private ISet<TMetadata>? _metadataSet;

    /// <summary>Initializes a new instance of the MetadataNode class.</summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    /// <param name="causes">The causes of the metadata.</param>
    public MetadataNode(
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        _metadataSource = metadata;
        _causes = causes;
    }

    /// <summary>Initializes a new instance of the MetadataNode class with a single metadata item.</summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    /// <param name="causes">The causes of the metadata.</param>
    public MetadataNode(TMetadata metadata, IEnumerable<BooleanResultBase<TMetadata>> causes)
        : this([metadata], causes)
    {
    }

    /// <summary>Initializes a new instance of the MetadataNode class for a leaf node with a single metadata item and no causes.</summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    internal MetadataNode(TMetadata metadata)
    {
        _underlying = EmptyUnderlying;
        _metadataSet = new HashSet<TMetadata> { metadata };
    }

    /// <summary>Gets the underlying metadata nodes.</summary>
    public IEnumerable<MetadataNode<TMetadata>> Underlying => _underlying ??= ResolveUnderlying(_metadataSource!, _causes!);

    /// <summary>Gets the metadata associated with this node.</summary>
    public IEnumerable<TMetadata> Metadata => _metadataSet ??= _metadataSource switch
    {
        ISet<TMetadata> metadataTier => metadataTier,
        _ => new HashSet<TMetadata>(_metadataSource!)
    };

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
            .DistinctWithOrderPreserved()
            .ToArray();

        var doesParentEqualChildAssertion = underlyingMetadata.SequenceEqual(metadata);

        return doesParentEqualChildAssertion
            ? underlying.SelectMany(result => result.Underlying)
            : underlying;
    }

    private string GetDebugDisplay()
    {
        var metadataSet = Metadata;
        return metadataSet switch
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
            _ when typeof(TMetadata).IsEnum => metadataSet.Serialize(),
            _ => base.ToString() ?? ""
        };
    }
}
