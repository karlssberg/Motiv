namespace Motiv.Traversal;

/// <summary>
/// An iterative post-order fold over a node tree, used in place of the non-tail recursion that the
/// result-tree and description-tree walks would otherwise use. The frames live on the heap, so the
/// depth a caller can walk is bounded by memory rather than by the thread's stack.
/// </summary>
/// <remarks>
/// Every walk in Motiv is a fold rather than a visit: each node's value is a function of its
/// children's values (and, for the assertion-source walks, of the node itself when its children
/// contribute nothing). Yielding nodes to a caller would leave that accumulation to be rebuilt by
/// hand at each call site, which is where the bugs would live.
/// </remarks>
internal static class PostOrderFold
{
    private const int InitialCapacity = 16;

    /// <summary>
    /// Folds the tree rooted at <paramref name="root" /> in post-order, memoising every value it
    /// computes through <paramref name="write" /> and pruning at every node <paramref name="read" />
    /// already has a value for.
    /// </summary>
    /// <param name="root">The node to fold.</param>
    /// <param name="descend">
    /// The children whose folded values <paramref name="combine" /> needs, in the order it needs them.
    /// Children omitted here are never folded, which is what preserves the pruning of the recursion
    /// this replaces.
    /// </param>
    /// <param name="combine">
    /// Produces a node's value from the values of the children <paramref name="descend" /> selected.
    /// The supplied list is a window over the fold's shared working buffer and is only valid for the
    /// duration of the call — materialise anything that outlives it.
    /// </param>
    /// <param name="read">The memoised value of a node, or <c>null</c> when it has none yet.</param>
    /// <param name="write">Stores a node's computed value.</param>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <typeparam name="TValue">The folded value type.</typeparam>
    /// <returns>The value of <paramref name="root" />.</returns>
    internal static TValue Fold<TNode, TValue>(
        TNode root,
        Func<TNode, IReadOnlyList<TNode>> descend,
        Func<TNode, IReadOnlyList<TValue>, TValue> combine,
        Func<TNode, TValue?> read,
        Action<TNode, TValue> write)
        where TNode : class
        where TValue : class
    {
        if (read(root) is { } alreadyFolded)
            return alreadyFolded;

        var frames = new Frame<TNode>[InitialCapacity];
        frames[0] = new Frame<TNode>(root, descend(root), values: 0);
        var depth = 1;

        var values = new List<TValue>();
        var window = new Window<TValue>(values);

        while (depth > 0)
        {
            ref var frame = ref frames[depth - 1];

            if (frame.HasUnvisitedChild)
            {
                var child = frame.TakeNextChild();

                if (read(child) is { } memoised)
                {
                    values.Add(memoised);
                    continue;
                }

                if (depth == frames.Length)
                    Array.Resize(ref frames, depth * 2);

                frames[depth++] = new Frame<TNode>(child, descend(child), values.Count);
                continue;
            }

            var node = frame.Node;
            var firstValue = frame.FirstValue;
            depth--;

            window.MoveTo(firstValue, values.Count - firstValue);
            var value = combine(node, window);

            values.RemoveRange(firstValue, values.Count - firstValue);
            write(node, value);
            values.Add(value);
        }

        return values[0];
    }

    private struct Frame<TNode>(TNode node, IReadOnlyList<TNode> children, int values)
    {
        private int _nextChild;

        public TNode Node { get; } = node;

        /// <summary>The index in the fold's working buffer at which this node's child values begin.</summary>
        public int FirstValue { get; } = values;

        public bool HasUnvisitedChild => _nextChild < children.Count;

        public TNode TakeNextChild() => children[_nextChild++];
    }

    /// <summary>
    /// A reusable view over a contiguous run of the fold's working buffer, so that handing a node its
    /// children's values costs no allocation per node.
    /// </summary>
    private sealed class Window<TValue>(List<TValue> values) : IReadOnlyList<TValue>
    {
        private int _offset;

        public int Count { get; private set; }

        public TValue this[int index] => values[_offset + index];

        public void MoveTo(int offset, int count)
        {
            _offset = offset;
            Count = count;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
                yield return values[_offset + i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
