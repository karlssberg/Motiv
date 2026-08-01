namespace Motiv.Serialization;

/// <summary>
/// The single coordinator a publish runs inside. Cascade has to be atomic across propositions *and*
/// rules — a live rule can sit in a proposition's dependent closure — so one object owns the layered
/// source, the write lock, the dependency graph, and the rebind participants.
/// </summary>
/// <remarks>
/// The lock and the version check solve different problems and both are needed. The lock is
/// machine-scale: it stops two publishes interleaving their graph walks. The version check
/// (compare-and-swap, as rules already do) is human-scale: it stops a save silently discarding an
/// edit made while a browser tab sat open.
/// </remarks>
internal sealed class BindingScope
{
    private readonly object _gate = new();
    private readonly Dictionary<NodeId, IRebindable> _participants = [];

    public BindingScope(SpecRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Overlay = new PropositionOverlay();
        Source = new LayeredSpecSource(Overlay, registry);
    }

    /// <summary>The immutable compiled catalog.</summary>
    public SpecRegistry Registry { get; }

    /// <summary>Who references whom.</summary>
    public DependencyGraph Graph { get; } = new();

    /// <summary>The live authored layer.</summary>
    public PropositionOverlay Overlay { get; }

    /// <summary>The live resolution order: authored first, then compiled.</summary>
    public ISpecSource Source { get; }

    /// <summary>Registers a node as rebindable. Replaces any participant already under that id.</summary>
    public void Enrol(IRebindable participant)
    {
        lock (_gate)
            _participants[participant.Node] = participant;
    }

    /// <summary>Unregisters a node, so it is no longer rebound.</summary>
    public void Withdraw(NodeId node)
    {
        lock (_gate)
            _participants.Remove(node);
    }

    /// <summary>Runs an action holding the write lock, so a publish sees a still graph.</summary>
    public T Locked<T>(Func<T> action)
    {
        lock (_gate)
            return action();
    }

    /// <summary>
    /// Runs an action holding the write lock. The companion to <see cref="Locked{T}"/> for the
    /// callers that produce no value, which would otherwise have to invent one to return.
    /// </summary>
    public void Locked(Action action)
    {
        lock (_gate)
            action();
    }

    /// <summary>
    /// Prepares every node transitively affected by republishing <paramref name="propositionName"/>,
    /// in dependency order, folding each prepared entry into <paramref name="prospective"/> so later
    /// members resolve the new definitions. Commits nothing.
    /// </summary>
    /// <returns>
    /// The dependents that would stop binding — empty when the whole closure prepared, in which case
    /// <paramref name="commits"/> holds every prepared rebind in the order it should be committed. On
    /// failure, both <paramref name="prospective"/> and <paramref name="commits"/> are left partially
    /// populated with whatever prepared successfully before the break was found, and the caller must
    /// discard both rather than act on either.
    /// </returns>
    public IReadOnlyList<BrokenDependent> PrepareClosure(
        string propositionName, PropositionOverlay prospective, List<IRebindCommit> commits)
    {
        var prospectiveSource = new LayeredSpecSource(prospective, Registry);
        var broken = new List<BrokenDependent>();

        foreach (var node in Graph.DependentClosure(propositionName))
        {
            // A graph edge can outlive its participant while a node is being torn down.
            if (!_participants.TryGetValue(node, out var participant))
                continue;

            var errors = new List<RuleError>();
            var commit = participant.PrepareRebind(prospectiveSource, errors);

            if (commit is null)
            {
                broken.Add(new BrokenDependent(
                    node.Name,
                    node.Kind == NodeKind.Rule ? "rule" : "proposition",
                    errors));
                // Keep going: reporting only the first break would make a wide failure take many
                // round trips to diagnose.
                continue;
            }

            if (commit.OverlayEntry is { } entry)
                prospective.Set(entry);

            commits.Add(commit);
        }

        return broken;
    }

    /// <summary>
    /// Publishes commits prepared earlier by <see cref="PrepareClosure"/>, folding each one's entry
    /// into the live overlay so the closure resolves to what it was rebound against. Deliberately
    /// separate from <see cref="PrepareClosure"/> — preparing every member before committing any of
    /// them is what makes a publish all-or-nothing — and called only once the caller has confirmed
    /// nothing broke. Commits cannot fail, so this cannot be interrupted part-way.
    /// </summary>
    /// <remarks>Runs under the write lock, like every other publish step, via the caller's <see cref="Locked{T}"/>.</remarks>
    public void CommitClosure(IReadOnlyList<IRebindCommit> commits)
    {
        foreach (var commit in commits)
        {
            commit.Commit();
            if (commit.OverlayEntry is { } entry)
                Overlay.Set(entry);
        }
    }
}
