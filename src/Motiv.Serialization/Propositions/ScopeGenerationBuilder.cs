namespace Motiv.Serialization;

/// <summary>
/// The only way to produce a <see cref="ScopeGeneration"/>. A builder forks the world it starts
/// from, is written into freely, and yields one successor — so a caller cannot publish half a change,
/// and "everything fallible runs before anything mutates" has somewhere to put the not-yet-mutated
/// state.
/// </summary>
/// <remarks>
/// <para>
/// Not thread-safe, and not meant to be: a builder is created, written and built inside one holder of
/// <see cref="BindingScope"/>'s inner monitor, or off to the side by a refresh that owns it alone.
/// </para>
/// <para>
/// <strong>Single-use.</strong> <see cref="Build"/> hands the generation these very collections
/// rather than copying them again, so a write that landed after building would edit a world that is
/// already published — the one thing <see cref="ScopeGeneration"/> exists to make impossible. Rather
/// than pay for a second copy on every publish, the builder refuses to be used twice.
/// </para>
/// </remarks>
internal sealed class ScopeGenerationBuilder
{
    private readonly SpecRegistry _registry;
    private readonly PropositionOverlay _overlay;
    private readonly DependencyGraph _graph;
    private readonly Dictionary<NodeId, IRebindable> _participants;
    private readonly Dictionary<string, AuthoredProposition> _authored;
    private RuleSlot?[] _ruleSlots;
    private StoreGeneration _sequence;
    private bool _built;

    /// <summary>Forks an existing world — the publish path, which changes a little and keeps the rest.</summary>
    public ScopeGenerationBuilder(SpecRegistry registry, ScopeGeneration from)
    {
        _registry = registry;
        _overlay = new PropositionOverlay(from.Overlay);
        _graph = new DependencyGraph(from.Graph);
        Source = new LayeredSpecSource(_overlay, registry);
        // Copied entry by entry rather than through a copy constructor: the source is an
        // IReadOnlyDictionary, and netstandard2.0's Dictionary has no constructor that takes one.
        _participants = [];
        foreach (var entry in from.Participants)
            _participants[entry.Key] = entry.Value;

        _authored = new Dictionary<string, AuthoredProposition>(StringComparer.Ordinal);
        foreach (var entry in from.Authored)
            _authored[entry.Key] = entry.Value;

        _ruleSlots = [.. from.RuleSlots];
        _sequence = from.Sequence;
    }

    /// <summary>
    /// Starts from nothing but the rules' shape — the refresh path, which rebuilds the authored world
    /// from the store rather than amending it. Slots are carried over in count only: a refresh rebinds
    /// every rule, and a slot index is stable for a rule's lifetime.
    /// </summary>
    public ScopeGenerationBuilder(SpecRegistry registry, int ruleCount)
    {
        _registry = registry;
        _overlay = new PropositionOverlay();
        _graph = new DependencyGraph();
        Source = new LayeredSpecSource(_overlay, registry);
        _participants = [];
        _authored = new Dictionary<string, AuthoredProposition>(StringComparer.Ordinal);
        _ruleSlots = new RuleSlot?[ruleCount];
        _sequence = StoreGeneration.Zero;
    }

    /// <summary>
    /// The prospective graph, written directly by the callers that track edges.
    /// </summary>
    /// <remarks>
    /// The getter is guarded rather than the writes, because the graph is mutable and handing it out
    /// is the only way to reach those writes — <c>builder.Graph.Set(…)</c> after <see cref="Build"/>
    /// is the mistake worth catching, and it cannot happen if the reference is never handed out
    /// afterwards.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The builder has already been built.</exception>
    public DependencyGraph Graph
    {
        get
        {
            ThrowIfBuilt();
            return _graph;
        }
    }

    /// <summary>
    /// Resolution as this prospective world would resolve — what a rebind binds against. Stable
    /// across the builder's life: it reads the same overlay instance every write lands in, so a
    /// caller may capture it once and still see everything folded in later.
    /// </summary>
    public ISpecSource Source { get; }

    public void SetOverlayEntry(SpecRegistryEntry entry)
    {
        ThrowIfBuilt();
        _overlay.Set(entry);
    }

    public void RemoveOverlayEntry(string name)
    {
        ThrowIfBuilt();
        _overlay.Remove(name);
    }

    public void SetAuthored(AuthoredProposition authored)
    {
        ThrowIfBuilt();
        _authored[authored.Name] = authored;
    }

    public void RemoveAuthored(string name)
    {
        ThrowIfBuilt();
        _authored.Remove(name);
    }

    public void Enrol(IRebindable participant)
    {
        ThrowIfBuilt();
        _participants[participant.Node] = participant;
    }

    public void Withdraw(NodeId node)
    {
        ThrowIfBuilt();
        _participants.Remove(node);
    }

    /// <summary>
    /// Publishes rebinds prepared earlier by <see cref="BindingScope.PrepareClosure"/> into this
    /// successor, so the whole closure goes live in the one swap that publishes it.
    /// </summary>
    /// <remarks>
    /// A rebind is now nothing but <see cref="IRebindCommit.ApplyTo"/>'s write into this builder —
    /// for a rule as much as for a proposition. That is what lets
    /// <see cref="BindingScope.PrepareClosure"/> apply the very same commits into a world it may yet
    /// discard: applying one moves no live state, so a rejected closure leaves nothing behind.
    /// </remarks>
    public void Apply(IReadOnlyList<IRebindCommit> commits)
    {
        ThrowIfBuilt();

        foreach (var commit in commits)
            commit.ApplyTo(this);
    }

    /// <summary>
    /// Grows the slot array so <paramref name="count"/> rules fit, and guards the builder against
    /// being written to after <see cref="Build"/> — which is why the three slot writers below need no
    /// <see cref="ThrowIfBuilt"/> of their own. Never shrinks: slots are permanent.
    /// </summary>
    private void EnsureRuleSlots(int count)
    {
        ThrowIfBuilt();

        if (_ruleSlots.Length >= count)
            return;

        var grown = new RuleSlot?[count];
        Array.Copy(_ruleSlots, grown, _ruleSlots.Length);
        _ruleSlots = grown;
    }

    /// <summary>Publishes a rule's binding, clearing any quarantine — see <see cref="RuleSlot.WithState"/>.</summary>
    public void SetRuleState(int slot, object state)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithState(state)
            : new RuleSlot(state, []);
    }

    /// <summary>
    /// Publishes a rule's re-bound implementation, <em>keeping</em> any quarantine — see
    /// <see cref="RuleSlot.WithBinding"/>. The cascade's write, and only the cascade's: a rebind
    /// changes what the rule's current document resolves to, which is not an answer to the question a
    /// quarantine asks.
    /// </summary>
    public void SetRuleBinding(int slot, object state)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithBinding(state)
            : new RuleSlot(state, []);
    }

    /// <summary>The state a slot will carry once this builder is published, or null when it has none.</summary>
    public object? FindRuleState(int slot) =>
        slot >= 0 && slot < _ruleSlots.Length ? _ruleSlots[slot]?.State : null;

    /// <summary>Records why a stored document could not be applied, keeping the binding in place.</summary>
    public void SetRuleQuarantine(int slot, IReadOnlyList<RuleError> quarantine)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithQuarantine(quarantine)
            : throw new InvalidOperationException(
                $"Rule slot {slot} has no state, so there is nothing to quarantine.");
    }

    /// <summary>Records where both stores stood. A refresh's write, which rebuilds from both at once.</summary>
    public void SetSequence(StoreGeneration sequence)
    {
        ThrowIfBuilt();
        _sequence = sequence;
    }

    /// <summary>
    /// Records where the <em>rule</em> store stands, leaving the proposition component as it was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per-component rather than a whole <see cref="StoreGeneration"/>, because every writer but a
    /// refresh touches exactly one store — the two are never written in the same transaction. A caller
    /// that had to assemble the pair itself would have to read the other component back out of the
    /// world it is forking and re-state it, and the day someone forgets, the untouched half silently
    /// resets to zero and the fencing token starts under-reporting a store nobody wrote to.
    /// </para>
    /// <para>
    /// <strong>For a load, not a publish.</strong> This states a position outright, which is only
    /// honest when the whole world being built was read from that store position — true of
    /// <see cref="RuleSet.Load"/> and <c>PropositionSet.Load</c>, its only two callers, and of nothing
    /// else. A publish has read one row, so it must go through <see cref="AdvanceRuleSequence"/> or
    /// <see cref="AdvancePropositionSequence"/> instead, whose remarks say what stating a position
    /// outright would cost it.
    /// </para>
    /// </remarks>
    public void SetRuleSequence(long generation)
    {
        ThrowIfBuilt();
        _sequence = _sequence with { Rules = generation };
    }

    /// <summary>
    /// Records where the <em>proposition</em> store stands, leaving the rule component as it was. See
    /// <see cref="SetRuleSequence"/>.
    /// </summary>
    public void SetPropositionSequence(long generation)
    {
        ThrowIfBuilt();
        _sequence = _sequence with { Propositions = generation };
    }

    /// <summary>
    /// Records where the rule store now stands after a publish of this world's own — but only if this
    /// world already stood where that store did before the publish wrote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>A world may only claim a store position it actually holds.</strong> The number a
    /// publish reads back is the store's <em>head</em>, and a head speaks for every row in the store,
    /// not just the one row this publish wrote. Any write another replica landed since this world was
    /// last built is therefore underneath that head — content this world does not have. Stamping it
    /// anyway is a lie in both directions the token is used: <see cref="StoreGeneration.MovedFrom"/>
    /// then reports nothing has moved on every subsequent poll, so the replica never rebuilds and
    /// serves the row it last read until some unrelated write happens to move the store again; and the
    /// response header tells a client it is looking at a world newer than the one it is actually being
    /// served, which is precisely the misrouting the header exists to expose.
    /// </para>
    /// <para>
    /// So the advance is conditional on <paramref name="before"/> — the head read before the publish
    /// wrote — matching where this world already stood. When it does, nothing foreign is underneath
    /// the new head and <paramref name="after"/> is honest. When it does not, the position is left
    /// alone: the store is genuinely ahead of this world, the next poll sees that and rebuilds, and
    /// the local publish this world just committed is read back from the store in the same pass. The
    /// cost of being wrong that way is one rebuild; the cost of being wrong the other way is a replica
    /// serving stale rows with a health check reading green.
    /// </para>
    /// <para>
    /// One gap survives deliberately: a foreign write landing between <paramref name="before"/> being
    /// read and this publish's own write is still underneath <paramref name="after"/> and still
    /// invisible. Closing it needs the store to report the position its own write produced, which
    /// <see cref="IRuleStore"/> and <see cref="IPropositionStore"/> do not offer. The window is one
    /// store round trip rather than a whole poll interval, and — unlike the case this guard closes —
    /// it does not depend on the replica being idle, which is the common state.
    /// </para>
    /// </remarks>
    /// <param name="before">Where the rule store stood before the publish wrote.</param>
    /// <param name="after">Where it stands now.</param>
    public void AdvanceRuleSequence(long before, long after)
    {
        ThrowIfBuilt();

        if (_sequence.Rules == before)
            _sequence = _sequence with { Rules = after };
    }

    /// <summary>
    /// The proposition-store counterpart of <see cref="AdvanceRuleSequence"/>, whose remarks carry the
    /// reasoning for both.
    /// </summary>
    /// <param name="before">Where the proposition store stood before the publish wrote.</param>
    /// <param name="after">Where it stands now.</param>
    public void AdvancePropositionSequence(long before, long after)
    {
        ThrowIfBuilt();

        if (_sequence.Propositions == before)
            _sequence = _sequence with { Propositions = after };
    }

    /// <summary>
    /// Seals the builder and yields the successor. Callable once — see the class remarks.
    /// </summary>
    /// <exception cref="InvalidOperationException">The builder has already been built.</exception>
    public ScopeGeneration Build()
    {
        ThrowIfBuilt();
        _built = true;
        return new ScopeGeneration(_registry, _sequence, _overlay, _graph, _participants, _authored, _ruleSlots);
    }

    private void ThrowIfBuilt()
    {
        if (_built)
            throw new InvalidOperationException(
                "This ScopeGenerationBuilder has already been built. Its collections now belong to a " +
                "published generation, so writing to it would edit a world other threads are reading.");
    }
}
