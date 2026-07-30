namespace Motiv.Serialization;

/// <summary>
/// Who references whom. Holds one outgoing-edge list per node plus a reverse index, and answers the
/// two questions a publish needs: would this edit close a cycle, and what must be rebound because of
/// it — in an order where every dependency precedes its dependents.
/// </summary>
/// <remarks>
/// Not synchronized: every mutation and query runs under the <see cref="BindingScope"/> write lock.
/// </remarks>
internal sealed class DependencyGraph
{
    private readonly Dictionary<NodeId, IReadOnlyList<string>> _outgoing = [];
    private readonly Dictionary<string, HashSet<NodeId>> _incoming = new(StringComparer.Ordinal);

    /// <summary>Replaces a node's outgoing references, keeping the reverse index consistent.</summary>
    public void Set(NodeId node, IReadOnlyList<string> references)
    {
        Detach(node);
        _outgoing[node] = references;
        foreach (var reference in references)
        {
            if (!_incoming.TryGetValue(reference, out var referrers))
                _incoming[reference] = referrers = [];
            referrers.Add(node);
        }
    }

    /// <summary>Drops a node and every edge leaving it.</summary>
    public void Remove(NodeId node)
    {
        Detach(node);
        _outgoing.Remove(node);
    }

    /// <summary>The nodes referencing the named proposition directly.</summary>
    public IReadOnlyList<NodeId> Referrers(string propositionName) =>
        _incoming.TryGetValue(propositionName, out var referrers) ? [.. referrers] : [];

    /// <summary>
    /// Every node transitively affected by republishing the named proposition, ordered so a node
    /// always follows the nodes it depends on. Excludes the named proposition itself.
    /// </summary>
    public IReadOnlyList<NodeId> DependentClosure(string propositionName)
    {
        // Reachable set first, by walking the reverse index breadth-first.
        var affected = new HashSet<NodeId>();
        var queue = new Queue<string>();
        queue.Enqueue(propositionName);

        while (queue.Count > 0)
        {
            foreach (var referrer in Referrers(queue.Dequeue()))
            {
                if (!affected.Add(referrer))
                    continue;
                // Only propositions are referenceable, so only they can carry the walk further.
                if (referrer.Kind == NodeKind.Proposition)
                    queue.Enqueue(referrer.Name);
            }
        }

        // Then order it. Reachability alone is not enough: a node may reference both the edited
        // proposition and another member of the closure, so breadth-first depth does not imply a
        // safe rebind order. Depth-first post-order over the closure's own edges does.
        var ordered = new List<NodeId>(affected.Count);
        var visited = new HashSet<NodeId>();
        foreach (var node in affected)
            Visit(node, affected, visited, ordered);

        return ordered;
    }

    /// <summary>
    /// The cycle the prospective references would create, as a path starting and ending at
    /// <paramref name="propositionName"/>, or null when they would not.
    /// </summary>
    public IReadOnlyList<string>? FindCycle(string propositionName, IReadOnlyList<string> prospectiveReferences)
    {
        foreach (var reference in prospectiveReferences)
        {
            var path = new List<string> { propositionName };
            if (Reaches(reference, propositionName, path, new HashSet<string>(StringComparer.Ordinal)))
                return path;
        }

        return null;
    }

    private void Detach(NodeId node)
    {
        if (!_outgoing.TryGetValue(node, out var previous))
            return;

        foreach (var reference in previous)
        {
            if (!_incoming.TryGetValue(reference, out var referrers))
                continue;
            referrers.Remove(node);
            if (referrers.Count == 0)
                _incoming.Remove(reference);
        }
    }

    /// <summary>Emits <paramref name="node"/> only after every closure member it depends on.</summary>
    private void Visit(NodeId node, HashSet<NodeId> closure, HashSet<NodeId> visited, List<NodeId> ordered)
    {
        if (!visited.Add(node))
            return;

        if (_outgoing.TryGetValue(node, out var references))
        {
            foreach (var reference in references)
            {
                var dependency = NodeId.Proposition(reference);
                if (closure.Contains(dependency))
                    Visit(dependency, closure, visited, ordered);
            }
        }

        ordered.Add(node);
    }

    /// <summary>Walks forward from <paramref name="from"/> looking for <paramref name="target"/>, recording the path.</summary>
    private bool Reaches(string from, string target, List<string> path, HashSet<string> visited)
    {
        path.Add(from);

        if (from == target)
            return true;

        if (visited.Add(from) && _outgoing.TryGetValue(NodeId.Proposition(from), out var references))
        {
            foreach (var reference in references)
            {
                if (Reaches(reference, target, path, visited))
                    return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }
}
