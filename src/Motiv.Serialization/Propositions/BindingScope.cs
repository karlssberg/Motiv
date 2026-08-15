namespace Motiv.Serialization;

/// <summary>
/// The single coordinator a publish runs inside. Cascade has to be atomic across propositions *and*
/// rules — a live rule can sit in a proposition's dependent closure — so one object owns the layered
/// source, the write lock, the dependency graph, and the rebind participants.
/// </summary>
/// <remarks>
/// <para>
/// Two tiers of exclusion cooperate here, plus a version check, and all three solve different
/// problems. The inner <see cref="_gate"/> monitor is machine-scale: it stops two publishes
/// interleaving their graph walks, but a monitor is released at the first <c>await</c>, so it
/// cannot hold across a store round trip. The outer <see cref="_outer"/> semaphore is what does
/// that — it serialises whole publish operations await-safely, and its real purpose is
/// cancellation: an answer to a store that has stopped responding, which the inner monitor cannot
/// give. The version check is human-scale: it stops a save silently discarding an edit made while a
/// browser tab sat open, and, for rules, is now also backed by a store primary key: every
/// <see cref="RuleSet"/> write ends in an <see cref="IRuleStore.AppendAsync"/> call, whose
/// <c>(Name, Version)</c> row is a cross-process compare-and-set on top of the in-process lock, not a
/// replacement for it — the lock still decides who gets to attempt the write; the store decides
/// whether that attempt actually lands.
/// </para>
/// <para>
/// <strong>The <c>Core</c> suffix names no single tier.</strong> Across <see cref="RuleSet"/>,
/// <c>PropositionSet</c> and <see cref="ChangeRequestSet"/> it always means "the caller already holds
/// an exclusion this method depends on, and must not re-acquire it" — but <em>which</em> one differs
/// by method, since the tiers are acquired at different call depths. <c>Prepare…Core</c> and
/// <c>Commit…Core</c> mostly assume the inner monitor, so they are callable from inside a synchronous
/// <see cref="Locked{T}"/> block; <c>PersistAndCommitCoreAsync</c>, <c>AppendCoreAsync</c> and
/// <c>CreateCoreAsync</c> assume the outer gate, so only <see cref="LockedAsync{T}"/> satisfies them
/// and a plain <see cref="Locked{T}"/> block does not. Read the method's own doc, which names its
/// lock; never infer the tier from the suffix.
/// </para>
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

    private readonly AsyncLocal<ScopeGeneration?> _pinned = new();
    private ScopeGeneration _current;
    private long _writes;

    public BindingScope(SpecRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _current = new ScopeGenerationBuilder(Registry, ruleCount: 0).Build();
        Source = new ScopeSource(this);
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

    /// <summary>The live resolution order: authored first, then compiled.</summary>
    public ISpecSource Source { get; }

    /// <summary>The live world. One volatile read; never edited in place.</summary>
    public ScopeGeneration Current => Volatile.Read(ref _current);

    /// <summary>
    /// The world this call resolves against: the pinned one when a <c>DecisionSnapshot</c> is open on
    /// this async flow, otherwise the live one.
    /// </summary>
    /// <remarks>
    /// <strong>Evaluation is pinned; administration is live.</strong> Only evaluation reads this, so
    /// that one decision sees one world rather than one world per rule. Everything else — binding a
    /// proposed document, listing the catalog, preparing or committing a publish — reads
    /// <see cref="Current"/> instead, because an administrative caller must see the truth, not the
    /// older world the request it happens to be serving was pinned to. A publish that prepared against
    /// a pinned world while committing into a successor forked from the live one would reintroduce
    /// exactly the staleness the write gate exists to prevent, and the pin is taken before the gate,
    /// so the gate could not catch it. Note that a bound spec holds direct references to whatever it
    /// resolved at bind time, so evaluation never reaches back through <see cref="Source"/>.
    /// </remarks>
    public ScopeGeneration Active => _pinned.Value ?? Current;

    /// <summary>
    /// How many times the world has been replaced. A refresh reads this <em>before</em> reading
    /// <see cref="Current"/>, rebuilds off to the side, and offers the result back to
    /// <see cref="TrySwap"/>, which refuses it if the stamp has moved — the world's own
    /// compare-and-set, and the reason a slow store need not hold the write gate. See
    /// <see cref="Publish"/> for why the read order is the reverse of the write order.
    /// </summary>
    public long WriteStamp => Volatile.Read(ref _writes);

    /// <summary>
    /// Builds a successor from the live world and swaps it in as one write. Assumes the inner monitor
    /// is held.
    /// </summary>
    public void Mutate(Action<ScopeGenerationBuilder> mutate)
    {
        var builder = new ScopeGenerationBuilder(Registry, Current);
        mutate(builder);
        Publish(builder.Build());
    }

    /// <summary>
    /// Swaps in a successor built elsewhere, unless the world moved since
    /// <paramref name="expectedWriteStamp"/> was taken. Assumes the inner monitor is held.
    /// </summary>
    /// <returns>Whether the successor went live.</returns>
    public bool TrySwap(ScopeGeneration successor, long expectedWriteStamp)
    {
        if (Volatile.Read(ref _writes) != expectedWriteStamp)
            return false;

        Publish(successor);
        return true;
    }

    /// <summary>Pins the live world for the current async flow. A nested pin reuses the outer one.</summary>
    public IDisposable Pin()
    {
        if (_pinned.Value is not null)
            return NestedPin.Instance;

        _pinned.Value = Current;
        return new OuterPin(this);
    }

    /// <summary>
    /// The one place the live world moves. World first, stamp second — and a refresh must read them
    /// the other way round, stamp first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dangerous reader is the one that sees the <em>old</em> world alongside the <em>new</em>
    /// stamp, because that pair makes a stale rebuild look current: it would build its successor from
    /// the world it read, present the stamp it read, and <see cref="TrySwap"/> would accept it —
    /// silently discarding the publish that moved the stamp. Writing the world first and reading the
    /// stamp first makes that pair unobservable. Every ordering that remains pairs a new stamp with a
    /// world at least as new, or an old stamp with anything at all, and an old stamp is refused.
    /// </para>
    /// <para>
    /// <see cref="Interlocked.Increment(ref long)"/> rather than a read-add-write through
    /// <see cref="Volatile"/>: every publisher is supposed to hold the inner monitor, but nothing
    /// enforces that, and the atomic costs the same as the non-atomic it replaces.
    /// </para>
    /// </remarks>
    private void Publish(ScopeGeneration successor)
    {
        Volatile.Write(ref _current, successor);
        Interlocked.Increment(ref _writes);
    }

    /// <summary>Registers a node as rebindable. Replaces any participant already under that id.</summary>
    public void Enrol(IRebindable participant) =>
        Locked(() => Mutate(builder => builder.Enrol(participant)));

    /// <summary>Unregisters a node, so it is no longer rebound.</summary>
    public void Withdraw(NodeId node) =>
        Locked(() => Mutate(builder => builder.Withdraw(node)));

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
    /// <param name="propositionName">The name whose dependents are being prepared.</param>
    /// <param name="prospective">The successor being built: bound against, and folded prepared entries into.</param>
    /// <param name="commits">Filled with every prepared rebind, in the order it should be committed.</param>
    /// <param name="excluding">
    /// Nodes that must not be rebound here even if the live graph names them as a dependent —
    /// see the remarks. Required rather than defaulted, so a caller cannot silently forget it: pass
    /// an empty set for a lone write, which has no other envelope members to protect.
    /// </param>
    /// <remarks>
    /// <para>
    /// A node's own prepared change is always authoritative over a rebind found here. This walk
    /// resolves <paramref name="propositionName"/>'s dependents from <c>Current.Graph</c> — the
    /// <em>live</em> edges, since nothing commits until the whole envelope's prepare phase has run —
    /// and rebinds each one from whatever <c>Current.Participants</c> currently holds, which is the
    /// pre-envelope definition (<see cref="Enrol"/> is only called by a commit). If a governed
    /// envelope is <em>also</em> explicitly publishing or withdrawing that same node elsewhere, that
    /// explicit edit already has its own prepared, correct result — a rebind built here from the
    /// stale definition would either overwrite that result at commit time (when both write the same
    /// node's live state, whichever runs last wins) or, more subtly, immediately overwrite the
    /// node's *fresh* entry in <paramref name="prospective"/> if that entry was folded in moments
    /// earlier, poisoning what every later envelope member resolves. <paramref name="excluding"/> is
    /// how a caller declares "this node is already spoken for": the walk still visits it (so its own
    /// dependents are still discovered and rebound — a node's exclusion from being *rebound* does not
    /// exclude anyone that references *it*), but skips building a commit for it and, critically, never
    /// touches its entry in <paramref name="prospective"/> — leaving whatever the node's own prepare
    /// already set there (or will set there, later in the same walk) untouched either way.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The dependents that would stop binding — empty when the whole closure prepared, in which case
    /// <paramref name="commits"/> holds every prepared rebind in the order it should be committed. On
    /// failure, both <paramref name="prospective"/> and <paramref name="commits"/> are left partially
    /// populated with whatever prepared successfully before the break was found, and the caller must
    /// discard both rather than act on either.
    /// </returns>
    public IReadOnlyList<BrokenDependent> PrepareClosure(
        string propositionName, ScopeGenerationBuilder prospective, List<IRebindCommit> commits,
        HashSet<NodeId> excluding)
    {
        var broken = new List<BrokenDependent>();

        foreach (var node in Current.Graph.DependentClosure(propositionName))
        {
            // This node's own prepared change — elsewhere in the same envelope — is authoritative;
            // a rebind built from its pre-envelope definition would only shadow it. Its own
            // dependents are unaffected: they are separate entries in this same closure, visited on
            // their own merits regardless of whether this node itself was skipped.
            if (excluding.Contains(node))
                continue;

            // A graph edge can outlive its participant while a node is being torn down.
            if (!Current.Participants.TryGetValue(node, out var participant))
                continue;

            var errors = new List<RuleError>();
            var commit = participant.PrepareRebind(prospective.Source, errors);

            if (commit is null)
            {
                broken.Add(new BrokenDependent(node.Name, node.KindLabel, errors));
                // Keep going: reporting only the first break would make a wide failure take many
                // round trips to diagnose.
                continue;
            }

            commit.ApplyTo(prospective);
            commits.Add(commit);
        }

        return broken;
    }

    private sealed class OuterPin(BindingScope scope) : IDisposable
    {
        public void Dispose() => scope._pinned.Value = null;
    }

    private sealed class NestedPin : IDisposable
    {
        public static NestedPin Instance { get; } = new();

        // The outer pin owns the lifetime; releasing here would end the decision early.
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A stable façade over an unstable world: <see cref="RuleSerializer"/> is built once and must
    /// keep resolving through whatever generation is live at the moment of the call.
    /// </summary>
    /// <remarks>
    /// <see cref="Current"/>, deliberately, not <see cref="Active"/>. This is the <em>binding</em>
    /// source — every caller of it is preparing or validating a document, which is administration —
    /// and administration is live. See <see cref="Active"/>'s remarks for what binding against a
    /// pinned world would cost.
    /// </remarks>
    private sealed class ScopeSource(BindingScope scope) : ISpecSource
    {
        public SpecRegistryEntry? Find(string name) => scope.Current.Source.Find(name);

        public CollectionBinding<TParent>? FindCollection<TParent>(string path) =>
            scope.Registry.FindCollection<TParent>(path);
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
