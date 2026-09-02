using Motiv.Traversal;

namespace Motiv.Shared;

/// <summary>Represents a node in the metadata hierarchy.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class MetadataNode<TMetadata>
{
    private static readonly MetadataNode<TMetadata>[] EmptyUnderlying = [];

    private readonly IEnumerable<TMetadata>? _metadataSource;
    private readonly IEnumerable<BooleanResultBase<TMetadata>>? _causes;

    /// <summary>
    /// The same causes as <see cref="_causes" />, and non-null only on a node whose metadata <i>is</i>
    /// their union. It is a discriminator rather than a second collection: <see cref="Metadata" />
    /// reads it to decide whether this level has anything of its own to build, while
    /// <see cref="_causes" /> is read by <see cref="Resolve" /> for the unrelated question of which
    /// levels the tier walk shows.
    /// </summary>
    private readonly IEnumerable<BooleanResultBase<TMetadata>>? _unionOfCauses;

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

    /// <summary>
    /// Initializes a node that carries no metadata of its own — its metadata is the union of its
    /// causes', which is the shape every composition's tier has.
    /// </summary>
    /// <param name="causes">The causes whose metadata this node is the union of.</param>
    /// <remarks>
    /// A node built this way computes <see cref="Metadata" /> by walking down to the causes that do
    /// carry metadata of their own, rather than by unioning each intervening level's set in turn. The
    /// two agree — union is associative and idempotent — but only the first lets a caller who reads
    /// one level avoid building every level beneath it, which over a fully-causal chain is the
    /// difference between an answer's own size and the square of it (ticket #195).
    /// </remarks>
    internal MetadataNode(IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        _causes = causes;
        _unionOfCauses = causes;

        // Not this node's metadata — Metadata never reads it on a union node. It is the lazy sequence
        // Resolve compares against its children to decide whether this level restates the one below,
        // so dropping it here would silently change which levels Underlying shows.
        _metadataSource = causes.GetValues();
    }

    /// <summary>Initializes a new instance of the MetadataNode class for a leaf node with a single metadata item and no causes.</summary>
    /// <param name="metadata">The metadata to associate with this node.</param>
    internal MetadataNode(TMetadata metadata)
    {
        _underlying = EmptyUnderlying;
        _metadataSet = new HashSet<TMetadata> { metadata };
    }

    /// <summary>Gets the underlying metadata nodes.</summary>
    public IEnumerable<MetadataNode<TMetadata>> Underlying =>
        _underlying ??= PostOrderFold.Fold(this, Descend, Combine, Read, Write);

    private MetadataNode<TMetadata>[]? _underlying;

    /// <summary>
    /// The tiers directly beneath this one, before the level-skipping that <see cref="Underlying" />
    /// applies.
    /// </summary>
    /// <remarks>
    /// <see cref="Underlying" /> drops a level that merely restates itself and returns what is beneath
    /// it, flattened. Both are correct for a walk over distinct levels, and both are fatal to one
    /// looking for each branch's own deepest tier: a branch whose deepest level <i>is</i> the dropped
    /// one leaves nothing behind, and a flat list cannot say which branch a tier came from. That is
    /// why the root-values walk descends here instead (ticket #189).
    /// </remarks>
    internal IReadOnlyList<MetadataNode<TMetadata>> Branches =>
        _causes is null ? [] : Resolved.Children;

    private Resolution<MetadataNode<TMetadata>> Resolved => field ??= Resolve(_metadataSource ?? [], _causes!);

    private static readonly Func<MetadataNode<TMetadata>, IReadOnlyList<MetadataNode<TMetadata>>> Descend =
        node => node.Resolved.Collapse ? node.Resolved.Children : [];

    private static readonly Func<
            MetadataNode<TMetadata>,
            IReadOnlyList<MetadataNode<TMetadata>[]>,
            MetadataNode<TMetadata>[]>
        Combine = (node, folded) => node.Resolved.Collapse
            ? folded.Flatten()
            : node.Resolved.Children;

    private static readonly Func<MetadataNode<TMetadata>, MetadataNode<TMetadata>[]?> Read =
        node => node._underlying;

    private static readonly Action<MetadataNode<TMetadata>, MetadataNode<TMetadata>[]> Write =
        (node, underlying) => node._underlying = underlying;

    /// <summary>Gets the metadata associated with this node.</summary>
    public IEnumerable<TMetadata> Metadata => _metadataSet ??= _unionOfCauses is null
        ? OwnMetadata()
        : UnionOf(_unionOfCauses);

    private ISet<TMetadata> OwnMetadata() =>
        _metadataSource switch
        {
            ISet<TMetadata> metadataTier => metadataTier,
            _ => new HashSet<TMetadata>(_metadataSource ?? [])
        };

    /// <summary>
    /// The distinct union of <paramref name="causes" />' metadata, gathered into a single set by
    /// descending past the levels that only union what is beneath them.
    /// </summary>
    /// <remarks>
    /// The descent is iterative for the same reason every other walk in Motiv is (Spec 3A), which is
    /// also why nothing needs to materialise these nodes bottom-up before one is read. Only an
    /// unmaterialised union is descended past; every other level hands over what it already holds, so
    /// reading a chain level by level still costs each level its own set — what it no longer does is
    /// charge that to a caller who read only the top.
    /// </remarks>
    private static ISet<TMetadata> UnionOf(IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        var union = new HashSet<TMetadata>();
        var pending = new Stack<MetadataNode<TMetadata>>();

        PushTiersOf(pending, causes);

        while (pending.Count > 0)
        {
            var node = pending.Pop();

            if (node is { _metadataSet: null, _unionOfCauses: { } deeper })
                PushTiersOf(pending, deeper);
            else
                union.UnionWith(node.Metadata);
        }

        return union;
    }

    /// <summary>
    /// Pushes the causes' tiers so that they pop left to right, which is the order the nested unions
    /// this descent replaces inserted their metadata in.
    /// </summary>
    private static void PushTiersOf(
        Stack<MetadataNode<TMetadata>> pending,
        IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        var causeList = causes.AsList();

        for (var i = causeList.Count - 1; i >= 0; i--)
            pending.Push(causeList[i].MetadataTier);
    }

    /// <summary>Returns a string that represents the current object.</summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => GetDebugDisplay();

    private static Resolution<MetadataNode<TMetadata>> Resolve(
        IEnumerable<TMetadata> metadata,
        IEnumerable<BooleanResultBase<TMetadata>> causes)
    {
        var causesArray = causes as BooleanResultBase<TMetadata>[] ?? causes.ToArray();

        var children = causesArray
            .SelectMany(cause =>
                cause switch
                {
                    IBooleanOperationResult<TMetadata> => cause.UnderlyingMetadataSources,
                    _ => cause.ToEnumerable()
                })
            .Select(cause => cause.MetadataTier)
            .ToArray();

        var childMetadata = children
            .SelectMany(metadataNode => metadataNode.Metadata)
            .DistinctWithOrderPreserved()
            .ToArray();

        return new Resolution<MetadataNode<TMetadata>>(children, childMetadata.SequenceEqual(metadata));
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
