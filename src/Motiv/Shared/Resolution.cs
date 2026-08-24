namespace Motiv.Shared;

/// <summary>
/// A node's direct children, and whether they say exactly what the node says — in which case the
/// node's level is collapsed and the children's own children take its place.
/// </summary>
/// <remarks>
/// Both the explanation tree and the metadata tier resolve their children this way. It is computed
/// once per node so that the fold's descend function and its combine step share one answer rather
/// than each deriving it.
/// </remarks>
/// <typeparam name="TNode">The tree's node type.</typeparam>
internal sealed class Resolution<TNode>(TNode[] children, bool collapse)
{
    public TNode[] Children { get; } = children;

    public bool Collapse { get; } = collapse;
}
