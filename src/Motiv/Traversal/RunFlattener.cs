namespace Motiv.Traversal;

/// <summary>
/// An iterative flatten of a run of nested nodes, used in place of the recursion a run walk would
/// otherwise use. The pending enumerators live on the heap, so the length a caller can flatten is
/// bounded by memory rather than by the thread's stack.
/// </summary>
/// <remarks>
/// Both renderers of a composition tree collapse a run of nested same-operation compositions into a
/// single operand list — the justification so that the run renders beneath one conjunction heading,
/// the reason so that it renders as one join. They differ only in where the run stops, which is the
/// <c>continuation</c> each supplies; the walk itself is this (Spec 3A / ticket 19).
/// </remarks>
internal static class RunFlattener
{
    /// <summary>
    /// Replaces every node that <paramref name="continuation" /> supplies operands for with those
    /// operands, repeatedly, and keeps every other node as it stands.
    /// </summary>
    /// <param name="nodes">The nodes the run starts from.</param>
    /// <param name="continuation">
    /// The operands a node contributes to the run in its own place, or <c>null</c> when the run stops
    /// there and the node itself is kept.
    /// </param>
    /// <typeparam name="TNode">The node type.</typeparam>
    /// <returns>The flattened run, in order.</returns>
    internal static List<TNode> Flatten<TNode>(
        IEnumerable<TNode> nodes,
        Func<TNode, IEnumerable<TNode>?> continuation)
    {
        var run = new List<TNode>();
        var pending = new Stack<IEnumerator<TNode>>();
        pending.Push(nodes.GetEnumerator());

        while (pending.Count > 0)
        {
            var current = pending.Peek();

            if (!current.MoveNext())
            {
                pending.Pop().Dispose();
                continue;
            }

            if (continuation(current.Current) is { } operands)
                pending.Push(operands.GetEnumerator());
            else
                run.Add(current.Current);
        }

        return run;
    }
}
