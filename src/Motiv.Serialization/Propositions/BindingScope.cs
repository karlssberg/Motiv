namespace Motiv.Serialization;

/// <summary>
/// The single coordinator a publish runs inside. Cascade has to be atomic across propositions *and*
/// rules — a live rule can sit in a proposition's dependent closure — so one object owns the layered
/// source, the write lock, the dependency graph, and the rebind participants.
/// </summary>
/// <remarks>
/// Two tiers of exclusion cooperate here, plus a version check, and all three solve different
/// problems. The inner <see cref="_gate"/> monitor is machine-scale: it stops two publishes
/// interleaving their graph walks, but a monitor is released at the first <c>await</c>, so it
/// cannot hold across a store round trip. The outer <see cref="_outer"/> semaphore is what does
/// that — it serialises whole publish operations await-safely, and its real purpose is
/// cancellation: an answer to a store that has stopped responding, which the inner monitor cannot
/// give. The version check is human-scale: it stops a save silently discarding an edit made while
/// a browser tab sat open, and today rests on the lock rather than a compare-and-swap — that will
/// move to a store primary key in a later task.
/// </remarks>
internal sealed class BindingScope
{
    private readonly object _gate = new();

    /// <summary>
    /// The outer tier: serialises whole publish operations await-safely. The inner
    /// <see cref="_gate"/> monitor is deliberately left in place rather than replaced — every
    /// <see cref="Enrol"/>/<see cref="Withdraw"/> site is reentrant, and a pure swap to a
    /// non-reentrant semaphore would self-deadlock at startup.
    /// </summary>
    /// <remarks>
    /// <strong>Acquired only at public entry points.</strong> <see cref="SemaphoreSlim"/> is not
    /// reentrant, so anything already inside must call a <c>…Core</c> method, never a public one.
    /// </remarks>
    private readonly SemaphoreSlim _outer = new(1, 1);

    private readonly Dictionary<NodeId, IRebindable> _participants = [];

    public BindingScope(SpecRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Overlay = new PropositionOverlay();
        Source = new LayeredSpecSource(Overlay, registry);
    }

    /// <summary>
    /// Opens a scope over a registry on behalf of a public constructor, recording the claim so the
    /// one pairing that would fail silently is refused here instead — see
    /// <see cref="SpecRegistry.ClaimScope"/> for which pairing, and why it has to be refused rather
    /// than reported later. Claiming before constructing is deliberate: a refused claim then leaves
    /// no half-built scope behind it.
    /// </summary>
    /// <param name="registry">The registry to open a scope over.</param>
    /// <param name="claim">Which kind of set is opening it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The registry is already claimed by the other kind.</exception>
    public static BindingScope For(SpecRegistry registry, ScopeClaim claim)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));

        registry.ClaimScope(claim);
        return new BindingScope(registry);
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
    /// Runs an operation holding the outer write gate, so a whole publish — including its store
    /// round trip — serialises against every other publish.
    /// </summary>
    /// <remarks>
    /// The reason this exists is <em>cancellation</em>, not throughput: the critical section is
    /// mostly CPU, so awaiting frees a few milliseconds at most. What it buys is
    /// <see cref="SemaphoreSlim.WaitAsync(CancellationToken)"/> — an answer to a store that has
    /// stopped responding, which a monitor cannot give.
    /// </remarks>
    public async Task<T> LockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _outer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _outer.Release();
        }
    }

    /// <summary>The void companion to <see cref="LockedAsync{T}"/>.</summary>
    public async Task LockedAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await _outer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _outer.Release();
        }
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
                broken.Add(new BrokenDependent(node.Name, node.KindLabel, errors));
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

/// <summary>Which kind of set opened a <see cref="BindingScope"/> over a <see cref="SpecRegistry"/>.</summary>
internal enum ScopeClaim
{
    /// <summary>No public constructor has opened a scope over the registry yet.</summary>
    None,

    /// <summary>A <see cref="RuleSet"/> was built from the registry.</summary>
    Rules,

    /// <summary>A <see cref="PropositionSet"/> was built from the registry.</summary>
    Propositions
}
