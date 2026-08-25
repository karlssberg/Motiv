using System.Diagnostics;

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
    private long _publishing;
    private PropositionSet? _propositions;
    private RuleSet? _rules;
    // The two halves of the last store sequence read, kept as separate longs rather than as a
    // StoreGeneration: the struct is two longs wide, so it has no atomic read, and Volatile cannot
    // take it. A reader can therefore catch the pair mid-update — a new rules number beside an old
    // propositions one — which for a lag gauge is a one-tick discrepancy that the next poll corrects,
    // and is why this is not used for anything a decision is made on.
    private long _lastStoreRules;
    private long _lastStorePropositions;

    public BindingScope(SpecRegistry registry)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _current = new ScopeGenerationBuilder(Registry, ruleCount: 0).Build();
        Source = new ScopeSource(this);

        // Held weakly, so a scope that goes out of scope stops being reported rather than leaking —
        // see TelemetrySubjects. Nothing is emitted unless a meter listener polls.
        MotivRulesTelemetry.Scopes.Add(this);
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
    /// <para>
    /// <strong>Writing is live; reading is pinned.</strong> That is the whole split, and it is drawn
    /// by what a caller <em>does</em> with the world, not by whether the caller is an administrator:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <strong>Binds or publishes → <see cref="Current"/>.</strong> Preparing or committing a publish,
    /// and binding a proposed document through <see cref="Source"/>. A publish that prepared against a
    /// pinned world while committing into a successor forked from the live one would reintroduce
    /// exactly the staleness the write gate exists to prevent — and the pin is taken before the gate,
    /// so the gate could not catch it. This is the hazard the split was originally written to settle.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Evaluates, or reads for display → <see cref="Active"/>.</strong> Evaluation
    /// (<c>Rule.Evaluate</c>, <c>AsyncRule.EvaluateAsync</c> and their policy-flavoured shadows), so
    /// one decision sees one world rather than one world per rule; and the read-only catalog
    /// listings — <c>PropositionSet.Propositions</c>, <c>Find</c>, <c>DocumentJsonOf</c>,
    /// <c>Dependents</c>, <em>and</em> <see cref="RuleSet.Rules"/>/<see cref="RuleSet.FindEntry"/>
    /// via <see cref="RuleBase.VersionedDocument"/> and <see cref="RuleBase.Quarantine"/> — which bind
    /// nothing and publish nothing. A pinned request stamps the generation it pinned onto its response
    /// as a fencing token, and that token is only worth trusting if the body came from the world the
    /// token names — so a listing read from <see cref="Current"/> under a pin would describe a world
    /// its own header disclaims, and <c>GET /rules</c> would disagree with <c>GET /propositions</c>
    /// served under the very same pin.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// An earlier wording of this — "evaluation is pinned; administration is live", with catalog
    /// listing named on the live side — has now sent two implementers the wrong way. The listings are
    /// administration by *who calls them* and evaluation by *what they do*, and it is the second that
    /// decides. Note also that a bound spec holds direct references to whatever it resolved at bind
    /// time, so evaluation never reaches back through <see cref="Source"/>.
    /// </para>
    /// <para>
    /// <strong>The public <see cref="RuleBase.Version"/> and <see cref="RuleBase.DocumentJson"/> stay
    /// on <see cref="Current"/>, and that is not an exception to the rule.</strong> They are what a
    /// writer reads back as the next <c>expectedVersion</c>, so they belong to the binds-or-publishes
    /// side; the number an endpoint <em>renders</em> comes from
    /// <see cref="RuleBase.VersionedDocument"/> instead, which is pinned. Two members, two acts, two
    /// worlds — the split is drawn per member, never per type.
    /// </para>
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

    /// <summary>The live world and the stamp that guards it, read in the only safe order.</summary>
    /// <remarks>
    /// <para>
    /// A rebuild that means to offer its result back to <see cref="TrySwap"/> must read both, and the
    /// order is not a matter of taste. Take the stamp first and the worst case is that a publish lands
    /// immediately afterwards, so the world read is newer than the stamp implies: the swap is refused
    /// and the rebuild retries. That is conservative — a wasted rebuild, never a wrong one. Take the
    /// world first and the publish lands in between, so the stamp is newer than the world: the swap is
    /// <em>accepted</em>, and it overwrites the publish with a world built before it. A needless retry
    /// is cheap; a silently discarded publish is the failure the stamp exists to prevent.
    /// </para>
    /// <para>
    /// This method exists so that ordering is a property of the code rather than of whoever read the
    /// documentation. <see cref="Current"/> and <see cref="WriteStamp"/> remain for callers that
    /// genuinely need only one of the two and are not going to swap.
    /// </para>
    /// </remarks>
    public (long Stamp, ScopeGeneration World) Snapshot()
    {
        var stamp = WriteStamp;
        return (stamp, Current);
    }

    /// <summary>How many rules are bound in the world being served — the rule half of the catalog.</summary>
    /// <remarks>
    /// Counted off the world rather than off the <see cref="RuleSet"/>, so it is the same world every
    /// other reading here is taken from. A slot is claimed by <c>RuleSet.Add</c> before the state is
    /// published into it, so a count taken mid-registration understates by the one rule still landing
    /// — the conservative direction for a gauge, and it corrects itself on the next poll.
    /// </remarks>
    public int RuleCount
    {
        get
        {
            var count = 0;
            foreach (var slot in Current.RuleSlots)
                if (slot is not null)
                    count++;
            return count;
        }
    }

    /// <summary>How many authored propositions the world being served holds — the other half of the catalog.</summary>
    public int PropositionCount => Current.Authored.Count;

    /// <summary>
    /// How far behind each store this replica was at its last refresh. Zero on both is converged.
    /// </summary>
    /// <remarks>
    /// Measured against the sequence the last <see cref="RefreshAsync"/> actually read, not against a
    /// fresh store read: a gauge callback fires on the exporter's schedule and must not issue a
    /// database round-trip per collection. So this is "how far behind was I when I last looked",
    /// which is the honest reading a poller can support — and a replica whose poller has stopped
    /// reports its last known lag rather than a comforting zero.
    /// </remarks>
    public (long Rules, long Propositions) Lag
    {
        get
        {
            var served = Current.Sequence;
            return (
                Behind(Volatile.Read(ref _lastStoreRules), served.Rules),
                Behind(Volatile.Read(ref _lastStorePropositions), served.Propositions));
        }
    }

    /// <summary>
    /// The gap, floored at zero. A served generation <em>ahead</em> of the last one read is ordinary —
    /// a publish on this replica moves the world without going through a refresh — and reporting it as
    /// negative lag would read as a replica ahead of its own store, which is not a thing.
    /// </summary>
    private static long Behind(long store, long served) => store > served ? store - served : 0;

    /// <summary>Records the proposition set that rebuilds this scope's authored half on a refresh.</summary>
    /// <remarks>
    /// <para>
    /// A scope is the unit a refresh rebuilds, not a set: an authored proposition and a rule that
    /// references it must reach a reader in the same swap, so whichever set a caller happens to hold
    /// has to be able to rebuild both halves. Recording the sets here is how the scope finds the other
    /// half. Called by the set's own constructor, so a scope is never half-joined once construction
    /// returns.
    /// </para>
    /// <para>
    /// Written and read through <see cref="Volatile"/>, like <see cref="Current"/> and
    /// <c>MotivRefreshService.LastReport</c>, and for the reason those are: the writer is whichever
    /// thread constructed the set, the reader is the poller, and no lock sits between them. The worst
    /// a stale read could produce is a rebuild that sees one half of a scope and aborts — not
    /// corruption, and unreachable through <c>AddMotivRules</c>, which resolves the proposition set
    /// first. It is done anyway because "a field crossing threads is volatile" is the convention here,
    /// and an unexplained exception to it reads as a deliberate one.
    /// </para>
    /// </remarks>
    public void Join(PropositionSet propositions) => Volatile.Write(ref _propositions, propositions);

    /// <summary>Records the rule set that rebuilds this scope's rule half on a refresh.</summary>
    /// <remarks>See <see cref="Join(PropositionSet)"/>.</remarks>
    public void Join(RuleSet rules) => Volatile.Write(ref _rules, rules);

    /// <summary>
    /// Rebuilds the whole world from both stores and swaps it in, if either store has moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately does <em>not</em> take the outer gate. Holding it across the store read would let
    /// a slow store block every publish, which is the hazard the async write contract exists to avoid.
    /// Instead the rebuild runs unlocked against a builder nobody else can see, and the swap validates
    /// under the monitor that <see cref="WriteStamp"/> has not moved — a compare-and-set on the world,
    /// mirroring how the store's <c>(Name, Version)</c> primary key guards a row.
    /// </para>
    /// <para>
    /// Two concurrent refreshes are safe and uninteresting: both build, one swaps, the other is told
    /// it was contended and retries. A concurrent <em>publish</em> is the interesting case, and it
    /// wins outright — see <see cref="TrySwap"/> for why a stamp comparison alone cannot see one, and
    /// why losing to it is the right outcome rather than a missed convergence.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancels the store reads.</param>
    /// <returns>What the refresh did, and anything that would not bind.</returns>
    public async Task<RefreshReport> RefreshAsync(CancellationToken cancellationToken)
    {
        // One read of each half per refresh, not one per use: a set joined halfway through would
        // otherwise have this poll the store for a half it then rebuilds without, or the reverse.
        var rules = Volatile.Read(ref _rules);
        var propositions = Volatile.Read(ref _propositions);

        // The last attempt's rebuild, so a run that exhausts its attempts still reports how long the
        // work it threw away took — a contended refresh is the expensive outcome, not the free one.
        var lastRebuildStart = 0L;

        // Three attempts, then leave it to the next tick. A refresh that loses the swap has been
        // overtaken by a publish, which is a world at least as new as the one it was building.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var sequence = new StoreGeneration(
                rules is null ? 0 : await rules.StoreGenerationAsync(cancellationToken).ConfigureAwait(false),
                propositions is null ? 0 : await propositions.StoreGenerationAsync(cancellationToken).ConfigureAwait(false));

            // Where the store stood when this replica last looked, which is what motiv.rules.replica_lag
            // is measured against. Written on every attempt, including the one that finds nothing moved:
            // that tick is exactly the evidence that the replica is level.
            Volatile.Write(ref _lastStoreRules, sequence.Rules);
            Volatile.Write(ref _lastStorePropositions, sequence.Propositions);

            // Snapshot() reads the stamp and the world in the only safe order — see its own doc.
            // Reading them separately here, in the wrong order, would hand this rebuild a stale
            // world with a fresh stamp, so its swap would succeed and silently overwrite a publish.
            var (stamp, current) = Snapshot();
            if (!sequence.MovedFrom(current.Sequence))
                return Report(RefreshReport.Unchanged(current.Sequence), rebuildStart: 0);

            var rebuildStart = Stopwatch.GetTimestamp();
            var builder = new ScopeGenerationBuilder(Registry, current.RuleSlots.Length);
            var regressions = new List<RefreshFailure>();
            var quarantined = new List<RefreshFailure>();

            // Propositions first: a rule document may reference an authored proposition, so the
            // authored layer has to be in the builder before any rule binds against it.
            if (propositions is not null)
                await propositions.RebuildIntoAsync(builder, current, regressions, quarantined, cancellationToken)
                    .ConfigureAwait(false);

            if (rules is not null)
                await rules.RebuildIntoAsync(builder, current, regressions, quarantined, cancellationToken)
                    .ConfigureAwait(false);

            if (regressions.Count > 0)
                return Report(RefreshReport.Aborted(current.Sequence, regressions, quarantined), rebuildStart);

            builder.SetSequence(sequence);

            if (Locked(() => TrySwap(builder.Build(), stamp)))
                return Report(RefreshReport.Applied(sequence, quarantined), rebuildStart);

            lastRebuildStart = rebuildStart;
        }

        return Report(RefreshReport.Contended(Current.Sequence), lastRebuildStart);
    }

    /// <summary>
    /// Counts the refresh, times the rebuild it ran, and hands the report back unchanged.
    /// </summary>
    /// <remarks>
    /// Wrapping every return rather than counting once at the top is what makes the outcome tag
    /// trustworthy: a refresh is only classified once it is over, and an early return that skipped
    /// this would leave the counter naming three outcomes for a method with four.
    /// </remarks>
    private static RefreshReport Report(RefreshReport report, long rebuildStart)
    {
        MotivRulesTelemetry.RecordRefresh(report.Outcome, rebuildStart);
        CountBindFailures(report.Regressions);
        CountBindFailures(report.Quarantined);
        return report;
    }

    /// <summary>
    /// Counts each failure under its own kind, since one refresh rebuilds both halves of the world
    /// into the same two lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Regressions and carried quarantines are counted alike. They differ in what the refresh
    /// <em>did</em> — one aborted it, the other rode along — and <see cref="RefreshReport"/> keeps
    /// them apart for exactly that reason. But this instrument answers a narrower question: how many
    /// stored documents will not bind in this build? Both are that, and an operator who has to add
    /// two series together to learn it is being made to reassemble something that was never usefully
    /// split.
    /// </para>
    /// <para>
    /// A document still broken on the next tick is counted again, deliberately: a rate that stays
    /// above zero is what tells an operator the replica is still stuck, where a count taken once
    /// would leave a wedged replica looking healthy as soon as it scrolled off the dashboard.
    /// </para>
    /// </remarks>
    private static void CountBindFailures(IReadOnlyList<RefreshFailure> failures)
    {
        if (failures.Count == 0)
            return;

        var rules = 0;
        foreach (var failure in failures)
            if (failure.Kind == MotivRulesTelemetry.RuleKind)
                rules++;

        MotivRulesTelemetry.RecordBindFailures(MotivRulesTelemetry.RuleKind, BindPhase.Refresh, rules);
        MotivRulesTelemetry.RecordBindFailures(
            MotivRulesTelemetry.PropositionKind, BindPhase.Refresh, failures.Count - rules);
    }

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
    /// <see cref="Mutate(Action{ScopeGenerationBuilder})"/> for a write that also yields a value — the
    /// version a publish minted, say. The companion to <see cref="Locked{T}"/>, and for the same
    /// reason: without it every such caller has to smuggle its result out through a captured local the
    /// compiler cannot prove was assigned, and then suppress it. Assumes the inner monitor is held.
    /// </summary>
    public T Mutate<T>(Func<ScopeGenerationBuilder, T> mutate)
    {
        var builder = new ScopeGenerationBuilder(Registry, Current);
        var result = mutate(builder);
        Publish(builder.Build());
        return result;
    }

    /// <summary>
    /// Swaps in a successor built elsewhere, unless the world moved since
    /// <paramref name="expectedWriteStamp"/> was taken — or is about to. Assumes the inner monitor is
    /// held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Two refusals, because the write stamp alone cannot see a publish that has not landed
    /// yet.</strong> A publish is <c>Locked{prepare}</c> → <c>await store</c> → <c>Locked{Mutate}</c>,
    /// and it holds <em>no monitor</em> across that await. The stamp only moves at the commit, so
    /// throughout the store round trip a publish is entirely invisible to a stamp comparison — and
    /// the successor it will commit was <em>prepared against the world as it stood before this
    /// swap</em>. Let the swap through and the publish resumes, forks the world this refresh just
    /// installed, and writes over it: its own publication, and — worse — every cascaded rebind in its
    /// closure, each carrying the pre-refresh document and version of a dependent, because a rebind
    /// deliberately carries the version across unchanged. The result is a world no store state ever
    /// held. It does not self-heal either: the publish then stamps the store position it read after
    /// its own write, so the next poll finds nothing has moved.
    /// </para>
    /// <para>
    /// So a publish in flight wins over a rebuild in flight, and it wins <em>before</em> it has done
    /// anything a stamp could record. The direction is the one the retry loop already handles: the
    /// refresh is told it was contended, and the world a publish leaves behind is always at least as
    /// new as the one this rebuild was assembling.
    /// </para>
    /// <para>
    /// Reading the counter under the monitor is what makes it sufficient rather than merely helpful.
    /// A publish increments it before taking the monitor to prepare, so any publish whose prepare has
    /// already run — the only kind that could commit a stale successor after this swap — released the
    /// monitor after incrementing, and this read acquires it afterwards. A publish that increments
    /// later than that has not prepared yet, so it will fork whatever world this swap installs, which
    /// is exactly what it should do.
    /// </para>
    /// </remarks>
    /// <returns>Whether the successor went live.</returns>
    public bool TrySwap(ScopeGeneration successor, long expectedWriteStamp)
    {
        if (Interlocked.Read(ref _publishing) != 0)
            return false;

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
    /// round trip — serialises against every other publish, and declares it in flight so a refresh
    /// cannot swap a world out from under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reason this exists is <em>cancellation</em>, not throughput: the critical section is
    /// mostly CPU, so awaiting frees a few milliseconds at most. What it buys is
    /// <see cref="SemaphoreSlim.WaitAsync(CancellationToken)"/> — an answer to a store that has
    /// stopped responding, which a monitor cannot give.
    /// </para>
    /// <para>
    /// <strong>This is also where a publish declares itself in flight.</strong> The gate is the one
    /// span every write path shares — the lone rule update and revert, the lone proposition create,
    /// update and withdraw, and the governed envelope all reach the store from inside it, and a
    /// <c>…Core</c> method that skips the gate does so only because a caller already holds it. So
    /// registering here is registering everywhere, by construction rather than by a list somebody has
    /// to keep up to date. <see cref="TrySwap"/>'s remarks say what the registration is for.
    /// </para>
    /// <para>
    /// Registered after the gate is acquired, not before: a publish still queued behind another has
    /// prepared nothing and could not commit a stale world, so counting it would only starve the
    /// refresh for longer. Deregistered in the <c>finally</c>, so a store that throws or a token that
    /// cancels leaves the count where it found it — a leaked registration would wedge refresh for the
    /// life of the process, which is a worse failure than the one this prevents.
    /// </para>
    /// </remarks>
    public async Task<T> LockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _outer.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _publishing);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _publishing);
            _outer.Release();
        }
    }

    /// <summary>The void companion to <see cref="LockedAsync{T}"/>.</summary>
    public async Task LockedAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await _outer.WaitAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _publishing);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _publishing);
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

        // One read, not one per use: the graph being walked, the participants being rebound and the
        // definitions they are rebound *from* all have to come from the same world, or a publish
        // landing mid-walk would have this rebind nodes one world names against definitions from
        // another. The inner monitor makes that unreachable today; reading twice would leave it a lock
        // away from being reachable. This is why the world is handed to PrepareRebind rather than
        // left for each participant to read for itself — a rule keeps its document and version in the
        // world, so a participant reading Scope.Current would be exactly that second read.
        var world = Current;

        foreach (var node in world.Graph.DependentClosure(propositionName))
        {
            // This node's own prepared change — elsewhere in the same envelope — is authoritative;
            // a rebind built from its pre-envelope definition would only shadow it. Its own
            // dependents are unaffected: they are separate entries in this same closure, visited on
            // their own merits regardless of whether this node itself was skipped.
            if (excluding.Contains(node))
                continue;

            // A graph edge can outlive its participant while a node is being torn down.
            if (!world.Participants.TryGetValue(node, out var participant))
                continue;

            var errors = new List<RuleError>();
            var commit = participant.PrepareRebind(prospective.Source, world, errors);

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
    /// source, and binding is the live side of the split — every caller of it is preparing or
    /// validating a document that may go on to be published. See <see cref="Active"/>'s remarks for
    /// what binding against a pinned world would cost.
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
