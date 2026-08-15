namespace Motiv.Serialization;

/// <summary>
/// The set of live rules an application executes. Adding a rule binds its default immediately
/// (fail-fast at startup); <see cref="UpdateAsync"/> and <see cref="RevertAsync"/> bind, persist,
/// and publish in that order — writers get optimistic version conflicts, evaluators always see a
/// coherent snapshot, and nothing is live unless it is also durable.
/// </summary>
/// <remarks>
/// Like <see cref="SpecRegistry"/>, registration (<see cref="Add"/>) is intended to finish at
/// startup before concurrent use; <see cref="UpdateAsync"/>/<see cref="RevertAsync"/>/lookups are
/// safe concurrently thereafter.
/// </remarks>
public sealed class RuleSet
{
    private readonly Dictionary<string, RuleBase> _rules = new(StringComparer.Ordinal);
    private readonly RuleSerializer _serializer;
    private readonly RuleSerializerOptions _options;
    private readonly IRuleStore _store;
    private bool _loaded;

    /// <summary>Creates a rule set whose documents bind against the given registry.</summary>
    /// <remarks>
    /// For a host that also authors propositions, build the rule set from the
    /// <see cref="PropositionSet"/> instead — see
    /// <see cref="RuleSet(PropositionSet, IRuleStore, RuleSerializerOptions)"/>. This overload opens a binding
    /// scope of its own, which a proposition set built from the same registry could never see into.
    /// </remarks>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="store">Where published rules persist between restarts, or null to keep them in memory only.</param>
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// A <see cref="PropositionSet"/> was already built from <paramref name="registry"/>, so this set
    /// could only ever be invisible to it.
    /// </exception>
    public RuleSet(SpecRegistry registry, IRuleStore? store = null, RuleSerializerOptions? options = null)
        : this(BindingScope.For(registry, ScopeClaim.Rules), store, options)
    {
    }

    /// <summary>
    /// Creates a rule set paired with a <see cref="PropositionSet"/>: the two share one binding
    /// scope, so a proposition edit and a rule update cannot interleave, and republishing a
    /// proposition rebinds every rule here that references it — all of them, or none.
    /// </summary>
    /// <remarks>
    /// This is the supported way to run rules and authored propositions together. Construct the
    /// proposition set first, register its models and <see cref="PropositionSet.Load"/> it, then
    /// build the rule set from it — <see cref="Add"/> binds a rule's default immediately, so any
    /// proposition that default references must already be live.
    /// </remarks>
    /// <param name="propositions">The proposition set these rules resolve authored propositions from.</param>
    /// <param name="store">Where published rules persist between restarts, or null to keep them in memory only.</param>
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propositions"/> is null.</exception>
    public RuleSet(PropositionSet propositions, IRuleStore? store = null, RuleSerializerOptions? options = null)
        : this((propositions ?? throw new ArgumentNullException(nameof(propositions))).Scope, store, options)
    {
    }

    /// <summary>
    /// Creates a rule set sharing a <see cref="BindingScope"/> with a <see cref="PropositionSet"/>, so
    /// a proposition edit and a rule update cannot interleave and a rule can be rebound by a
    /// proposition's republication.
    /// </summary>
    internal RuleSet(BindingScope scope, IRuleStore? store = null, RuleSerializerOptions? options = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _store = store ?? new InMemoryRuleStore();
        _options = options ?? new RuleSerializerOptions();
        _serializer = new RuleSerializer(scope.Source, _options);
    }

    /// <summary>The coordinator this set publishes under.</summary>
    internal BindingScope Scope { get; }

    /// <summary>The number of registered rules.</summary>
    public int Count => _rules.Count;

    /// <summary>Read-only listings of every registered rule, reflecting live versions.</summary>
    public IReadOnlyCollection<RuleSetEntry> Rules =>
        _rules.Values.Select(ToEntry).ToArray();

    /// <summary>
    /// Registers a rule and binds its default immediately — an invalid default document throws
    /// here, at startup, rather than at first evaluation.
    /// </summary>
    /// <param name="rule">The rule to register.</param>
    /// <returns>This rule set, to allow chained registration.</returns>
    /// <exception cref="RuleSerializationException">The rule's default document does not bind.</exception>
    public RuleSet Add(RuleBase rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));

        return Scope.Locked(() =>
        {
            if (_rules.ContainsKey(rule.Name))
                throw new ArgumentException($"A rule is already registered under the name '{rule.Name}'.", nameof(rule));

            try
            {
                rule.Attach(_serializer);
            }
            catch (RuleSerializationException ex)
            {
                // Name the failing rule — a startup failure over many rules is otherwise anonymous.
                throw new RuleSerializationException($"Rule '{rule.Name}': {ex.Message}", ex.Errors);
            }

            _rules[rule.Name] = rule;
            Track(rule);
            return this;
        });
    }

    /// <summary>Looks up a registered rule by name.</summary>
    /// <param name="name">The rule name.</param>
    /// <returns>The rule, or null when none is registered under the name.</returns>
    public RuleBase? Find(string name) => _rules.TryGetValue(name, out var rule) ? rule : null;

    /// <summary>
    /// Looks up one rule's listing by name. The entry's version and document come from a single
    /// snapshot, so they are always coherent even while the rule is being replaced.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <returns>The entry, or null when no rule is registered under the name.</returns>
    public RuleSetEntry? FindEntry(string name) =>
        Find(name) is { } rule ? ToEntry(rule) : null;

    private static RuleSetEntry ToEntry(RuleBase rule)
    {
        var (version, documentJson) = rule.VersionedDocument();
        return new RuleSetEntry(
            rule.Name, rule.ModelType, rule.MetadataType, rule.IsAsync, rule.IsPolicy,
            version, rule.Description, documentJson)
        {
            Quarantine = rule.Quarantine
        };
    }

    /// <summary>
    /// Reads every stored rule head and applies it over the compiled default. A document that no
    /// longer binds is <em>quarantined</em> rather than fatal: the rule keeps evaluating its compiled
    /// default, its stored version is preserved so a repair can address it, and the reason is reported
    /// on the returned <see cref="RuleLoadReport"/>.
    /// </summary>
    /// <remarks>
    /// Call once, after every <see cref="Add"/>, and after the paired <see cref="PropositionSet.Load"/>
    /// — a stored rule document may reference an authored proposition. Synchronous by design: startup
    /// is, and the DI factory wall cannot await.
    /// </remarks>
    /// <returns>What was quarantined, and what was orphaned.</returns>
    /// <exception cref="InvalidOperationException">Load has already been called on this set.</exception>
    public RuleLoadReport Load() =>
        Scope.Locked(() =>
        {
            // Same precondition as PropositionSet.Load, for the same reason: a second pass over rows
            // that bound the first time and quarantine the second would leave the catalog reporting a
            // rule broken while the evaluator still resolved the stale binding. A refresh has to be a
            // whole rebuild, so refuse rather than half-do it.
            if (_loaded)
                throw new InvalidOperationException(
                    "Load has already been called on this RuleSet. It reads the store once, at " +
                    "startup; it is not a refresh.");

            // Set only once the store has been read: reading is the one step that can throw rather
            // than quarantine, and it mutates nothing, so an unreachable store leaves the set loadable.
            var heads = _store.Load() ?? [];
            _loaded = true;

            var quarantined = new List<QuarantinedRule>();
            var orphaned = new List<string>();

            foreach (var head in heads)
            {
                // A quarantine is recorded on the rule it names, so a row with no usable name has
                // nowhere to be recorded and skipping it is the only non-fatal option.
                if (head?.Name is null)
                    continue;

                if (Find(head.Name) is not { } rule)
                {
                    // History outlives the code that produced it. Not a fault, and not a quarantine.
                    orphaned.Add(head.Name);
                    continue;
                }

                if (Apply(rule, head) is { } errors)
                    quarantined.Add(new QuarantinedRule(head.Name, head.Version, errors));
            }

            return new RuleLoadReport(quarantined, orphaned);
        });

    /// <summary>
    /// Applies one stored head over a rule's compiled default, returning the errors that quarantined
    /// it or null when it bound. A null document is a recorded revert, not an absent row: the rule
    /// stays on its default and only the version moves.
    /// </summary>
    private IReadOnlyList<RuleError>? Apply(RuleBase rule, StoredRule head)
    {
        // The expected version is read from the rule itself. This is a load, not a concurrent write —
        // there is no caller holding a stale number — so the only outcome worth distinguishing is
        // whether the document binds.
        var prepared = head.DocumentJson is null
            ? PrepareRevertCore(head.Name, expectedVersion: rule.Version)
            : PrepareUpdateCore(head.Name, head.DocumentJson, expectedVersion: rule.Version);

        if (prepared.Publication is { } publication)
        {
            // Committed directly, not through the persisting write path: this document came *from*
            // the store, so appending it again would mint a duplicate version row and conflict on its
            // own primary key.
            CommitCore(head.Name, publication);
        }
        else
        {
            // The rule stays on its compiled default — a rule must be able to evaluate, and there is
            // nothing else to bind — but says so rather than reverting silently.
            rule.Quarantine = prepared.Errors;
        }

        // Either way the store's version is authoritative: a restart must not renumber history, and a
        // repair must be addressed against the version the store actually holds, not the one this
        // publish just minted or the one Add bound, or the very first repair attempt would conflict.
        rule.RestoreVersion(head.Version);

        return prepared.Publication is null ? prepared.Errors : null;
    }

    /// <summary>
    /// Replaces a rule's implementation with a document: bind → persist → publish, under the outer
    /// write gate. The live rule is untouched unless the document binds, the expected version holds,
    /// <em>and</em> the store accepts the new version row.
    /// </summary>
    /// <remarks>
    /// The ordering is the whole guarantee. Binding and persisting are the two steps that can fail and
    /// both run before anything mutates, so a broken document or a version another replica already
    /// took leaves nothing live — there is no rollback here because none is needed.
    /// </remarks>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="provenance">Who is publishing, and why. Written into the version log.</param>
    /// <param name="cancellationToken">Cancels while waiting for the gate or the store.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public Task<RuleUpdateResult> UpdateAsync(
        string name, string documentJson, int expectedVersion, RuleChangeProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        return Scope.LockedAsync(
            () => PersistAndCommitCoreAsync(
                name, () => PrepareUpdateCore(name, documentJson, expectedVersion), provenance, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Reverts a rule to its default. The version moves forward, never back, and the log records that
    /// the rule went back to code.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="provenance">Who is reverting, and why. Written into the version log.</param>
    /// <param name="cancellationToken">Cancels while waiting for the gate or the store.</param>
    /// <returns>The outcome: updated, version conflict, invalid default document, or not found.</returns>
    public Task<RuleUpdateResult> RevertAsync(
        string name, int expectedVersion, RuleChangeProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        return Scope.LockedAsync(
            () => PersistAndCommitCoreAsync(
                name, () => PrepareRevertCore(name, expectedVersion), provenance, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Restores the document a previous version carried by <em>appending</em> a copy of it. Restoring
    /// v5 writes v9 — history is never rewritten, and the new row is itself the record that a rollback
    /// happened: when <paramref name="provenance"/> carries no <see cref="RuleChangeProvenance.ChangeNote"/>
    /// of its own, this fills in one naming <paramref name="targetVersion"/>, so the appended row reads
    /// as a restore rather than a document that merely happens to match an old one.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="targetVersion">The version whose document to republish.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="provenance">Who is rolling back, and why.</param>
    /// <param name="cancellationToken">Cancels the history read and the publish.</param>
    /// <returns>
    /// The outcome. <see cref="RuleUpdateOutcome.NotFound"/> when the rule or the target version is
    /// unknown — a version that was never recorded cannot be restored.
    /// </returns>
    public async Task<RuleUpdateResult> RestoreAsync(
        string name, int targetVersion, int expectedVersion, RuleChangeProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        // Read history outside the gate: it is a store read that cannot affect anything live, and
        // holding an exclusion gate across it would serialise publishes behind an I/O round trip.
        var history = await _store.HistoryAsync(name, cancellationToken).ConfigureAwait(false);
        var target = history.FirstOrDefault(row => row.Version == targetVersion);
        if (target is null)
            return RuleUpdateResult.NotFound();

        // The caller's own note always wins — this only fills the gap when none was given, so the
        // log still records a rollback even when the caller supplied nothing more than "who".
        var stamped = string.IsNullOrWhiteSpace(provenance.ChangeNote)
            ? provenance with { ChangeNote = $"Restored from version {targetVersion}." }
            : provenance;

        return target.DocumentJson is null
            ? await RevertAsync(name, expectedVersion, stamped, cancellationToken).ConfigureAwait(false)
            : await UpdateAsync(name, target.DocumentJson, expectedVersion, stamped, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>Every recorded version of one rule, oldest first.</summary>
    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken = default) =>
        _store.HistoryAsync(name, cancellationToken);

    /// <summary>
    /// The middle of bind → persist → publish, for a caller already holding the outer gate. The store
    /// call is the last step that can fail; everything after it is a memory swap that cannot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Holds <em>both</em> exclusion tiers, not just the outer gate. <see cref="BindingScope"/>'s
    /// outer <see cref="SemaphoreSlim"/> (taken by <see cref="BindingScope.LockedAsync{T}"/>, which
    /// every caller of this method is already inside) serialises whole write operations await-safely,
    /// but it does not exclude the synchronous read surface — <c>PropositionSet.Propositions</c>,
    /// <c>PropositionSet.Find</c>, <c>PropositionSet.DocumentJsonOf</c>, <c>PropositionSet.Dependents</c>
    /// — nor <see cref="Add"/> or <see cref="Load"/>, all of which take only the inner
    /// <see cref="BindingScope.Locked{T}"/> monitor and always will: a synchronous reader cannot await
    /// the semaphore without either blocking a thread or becoming async itself, and both defeat the
    /// point of a lock-free-shaped read surface. A semaphore and a monitor do not exclude each other,
    /// so a write that took only the semaphore could run <see cref="CommitCore"/> → <see cref="Track"/>
    /// → <c>Scope.Graph.Set</c>/<c>Remove</c> concurrently with any of those monitor-only readers
    /// walking the same, deliberately-unsynchronized <c>DependencyGraph</c> or <c>PropositionOverlay</c>
    /// mid-mutation. Prepare needs the monitor too, not only commit: binding a document reads through
    /// <c>Scope.Source</c> (the layered proposition overlay), which a concurrent monitor-held
    /// proposition edit could be mutating mid-read.
    /// </para>
    /// <para>
    /// The store round trip is the one span that must sit outside the monitor — holding a
    /// <see cref="System.Threading.Monitor"/> across an <c>await</c> is never safe, since the monitor
    /// is thread-affine and the continuation may resume on a different thread. So the shape is:
    /// outer gate (already held) → <see cref="BindingScope.Locked{T}"/> { <paramref name="prepare"/> }
    /// → <c>await</c> the store, unlocked → <see cref="BindingScope.Locked{T}"/> { <see cref="CommitCore"/> }.
    /// The monitor is reentrant, so this composes with <see cref="Track"/>'s own
    /// <see cref="BindingScope.Enrol"/>/<see cref="BindingScope.Withdraw"/> calls.
    /// </para>
    /// </remarks>
    internal async Task<RuleUpdateResult> PersistAndCommitCoreAsync(
        string name, Func<RulePrepareResult> prepare, RuleChangeProvenance provenance,
        CancellationToken cancellationToken)
    {
        var prepared = Scope.Locked(prepare);
        if (prepared.Publication is not { } publication)
            return prepared.ToFailureResult();

        var appended = await _store
            .AppendAsync([RowFor(name, publication, provenance)], cancellationToken)
            .ConfigureAwait(false);

        if (appended.IsConflict)
            return RuleUpdateResult.VersionConflict(appended.CurrentVersion);

        // Nothing below can fail. CommitCore also clears any quarantine on the rule — a successful
        // publish is exactly the repair that resolves one.
        return Scope.Locked(() => CommitCore(name, publication));
    }

    /// <summary>
    /// Appends version rows for several prepared publications as one store call, so a governed
    /// envelope's rule half lands all-or-nothing. Assumes the outer gate is held; commits nothing —
    /// the caller commits each prepared publication separately once the whole envelope has persisted.
    /// </summary>
    internal Task<RuleAppendResult> AppendCoreAsync(
        IReadOnlyList<(string Name, IRulePublication Publication)> prepared,
        RuleChangeProvenance provenance, CancellationToken cancellationToken) =>
        _store.AppendAsync(
            [.. prepared.Select(entry => RowFor(entry.Name, entry.Publication, provenance))],
            cancellationToken);

    /// <summary>Builds the version row a prepared publication will be recorded as.</summary>
    internal static StoredRuleVersion RowFor(
        string name, IRulePublication publication, RuleChangeProvenance provenance)
    {
        var stamped = provenance.WithDefaults();
        return new StoredRuleVersion(
            name, publication.Version, publication.DocumentJson,
            stamped.Author, DateTimeOffset.UtcNow,
            stamped.ChangeNote, stamped.ApprovalRef, stamped.BuildId);
    }

    /// <summary>
    /// Prepares an update without publishing it, for a caller already holding
    /// <see cref="BindingScope"/>'s inner monitor. The caller persists the prepared version, then
    /// commits — see <see cref="CommitCore"/>. The split exists so that everything fallible runs
    /// before anything mutates.
    /// </summary>
    /// <remarks>
    /// Two <c>Core</c> suffixes meet here and mean different things — see <see cref="BindingScope"/>'s
    /// class remarks. This method's is the narrower one: every caller reaches it from inside
    /// <see cref="PersistAndCommitCoreAsync"/>'s own <see cref="BindingScope.Locked{T}"/> call, so what
    /// it assumes held is the monitor, not the gate — the same caller may well hold the gate too, but
    /// nothing here depends on that. <see cref="PersistAndCommitCoreAsync"/>'s own suffix is the lock
    /// split: the outer gate (<see cref="BindingScope.LockedAsync{T}"/>) is a
    /// <see cref="SemaphoreSlim"/> and therefore <em>not</em> reentrant, so calling
    /// <see cref="UpdateAsync"/> from inside it would self-deadlock with no error. That split is about
    /// the *unit of atomicity* — a caller that means "these edits publish together" must not be able to
    /// express it as a series of calls each of which releases the gate in between.
    /// <c>Prepare</c>/<c>Commit</c> is a third, separate split, about where a step that can still fail —
    /// the store write — is allowed to sit.
    /// </remarks>
    internal RulePrepareResult PrepareUpdateCore(string name, string documentJson, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.PrepareUpdate(_serializer, documentJson, expectedVersion)
            : RulePrepareResult.NotFound();

    /// <summary>Prepares a revert without publishing it. See <see cref="PrepareUpdateCore"/>.</summary>
    internal RulePrepareResult PrepareRevertCore(string name, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.PrepareRevert(_serializer, expectedVersion)
            : RulePrepareResult.NotFound();

    /// <summary>
    /// <see cref="PrepareUpdateCore(string,string,int)"/> against an explicit source rather than the
    /// live one, so a governed envelope's rule half can bind against a prospective overlay carrying
    /// propositions the same envelope creates or changes — the live overlay does not hold them yet,
    /// because nothing commits until the whole envelope has persisted. Mirrors
    /// <see cref="ValidateCore"/>, which takes the same kind of source for the same reason.
    /// </summary>
    internal RulePrepareResult PrepareUpdateCore(
        string name, string documentJson, int expectedVersion, ISpecSource source) =>
        Find(name) is { } rule
            ? rule.PrepareUpdate(new RuleSerializer(source, _options), documentJson, expectedVersion)
            : RulePrepareResult.NotFound();

    /// <summary>The prospective-source counterpart to <see cref="PrepareRevertCore(string,int)"/>. See <see cref="PrepareUpdateCore(string,string,int,ISpecSource)"/>.</summary>
    internal RulePrepareResult PrepareRevertCore(string name, int expectedVersion, ISpecSource source) =>
        Find(name) is { } rule
            ? rule.PrepareRevert(new RuleSerializer(source, _options), expectedVersion)
            : RulePrepareResult.NotFound();

    /// <summary>
    /// Commits a prepared publication and re-tracks the rule's graph edges. Has no failure outcome —
    /// everything a caller can get wrong was already decided by the prepare. Assumes
    /// <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    internal RuleUpdateResult CommitCore(string name, IRulePublication publication)
    {
        // Resolved before the commit so that the unreachable arm fails with nothing yet moved.
        // A publication only exists because a Prepare found the rule, the monitor has been held
        // throughout, and rules are never unregistered — so this cannot miss. It throws rather than
        // skipping the tracking below, which would leave the rule published with stale graph edges.
        var rule = Find(name)
            ?? throw new InvalidOperationException(
                $"Rule '{name}' is no longer registered, so its prepared publication cannot be committed.");

        publication.Commit();

        // A quarantine says "running a compiled default in place of a stored document that would not
        // bind". A successful publish is exactly what stops that being true, so it must not outlive
        // one — an operator who repairs a rule would otherwise be told it is still broken until the
        // process restarts.
        rule.Quarantine = [];

        // Track reads the rule's *current* document, so it must run after the commit, not before.
        Track(rule);

        return RuleUpdateResult.Updated(publication.Version);
    }

    /// <summary>
    /// Binds a proposed document against <paramref name="source"/> without publishing anything, so a
    /// governed publish can discover that a rule half of an envelope would not bind while nothing
    /// has moved yet. Passing a prospective source is the point: an envelope's rule edit may
    /// reference a proposition the same envelope creates, which the live source cannot resolve.
    /// Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The proposed document.</param>
    /// <param name="source">The source names resolve against — live, or prospective.</param>
    /// <returns>Why the document would not bind, or empty when it would.</returns>
    internal IReadOnlyList<RuleError> ValidateCore(string name, string documentJson, ISpecSource source)
    {
        if (Find(name) is not { } rule)
            return [];

        var errors = new List<RuleError>();
        rule.ValidateDocument(new RuleSerializer(source, _options), documentJson, errors);
        return errors;
    }

    /// <summary>
    /// The names a proposed document would resolve, so a governed publish can tell which of its own
    /// members would reference a proposition it is also withdrawing. The document must already have
    /// passed <see cref="ValidateCore"/>, which rules a parse failure out.
    /// </summary>
    internal IReadOnlyList<string> ReferencesOfCore(string? documentJson) => ReferencesOf(documentJson);

    /// <summary>
    /// The names a rule would resolve once reverted. A compiled default resolves none, but a rule
    /// declared with a <see cref="RuleDocumentSource"/> default re-acquires that document's
    /// references — <see cref="Track"/> recomputes them from whatever document the revert published,
    /// so a revert is not automatically a departure from the graph.
    /// </summary>
    internal IReadOnlyList<string> DefaultReferencesOfCore(string name) =>
        Find(name) is { } rule ? ReferencesOf(rule.Default.DocumentJson) : [];

    /// <summary>
    /// Binds the document a revert would republish against <paramref name="source"/> without
    /// publishing — the counterpart of <see cref="ValidateCore"/> for the deletion arm of a governed
    /// publish. A revert is not a return to something known-good: <see cref="RuleBase.PrepareRevert"/>
    /// re-binds the default against whatever the world looks like now, and a proposition edit landing
    /// earlier in the same envelope can be exactly what stops it binding.
    /// </summary>
    /// <remarks>
    /// A compiled default is skipped rather than checked. It resolves no names and was type-checked
    /// at construction, so <see cref="RuleBase.PrepareRevert"/> cannot fail on it.
    /// </remarks>
    /// <param name="name">The rule name.</param>
    /// <param name="source">The source names resolve against — live, or prospective.</param>
    /// <returns>Why the default would not bind, or empty when it would.</returns>
    internal IReadOnlyList<RuleError> ValidateDefaultCore(string name, ISpecSource source) =>
        Find(name) is { Default.DocumentJson: { } documentJson }
            ? ValidateCore(name, documentJson, source)
            : [];

    /// <summary>
    /// Records the rule's current outgoing references and its participation in rebinds. A rule on a
    /// compiled default resolves no names, so it leaves the graph entirely.
    /// </summary>
    private void Track(RuleBase rule)
    {
        var node = NodeId.Rule(rule.Name);
        var references = ReferencesOf(rule.DocumentJson);

        // One builder, not a graph write and an enrolment that each swap: the edges and the
        // participant they name have to reach a reader together or not at all.
        Scope.Mutate(builder =>
        {
            if (references.Count == 0)
            {
                builder.Graph.Remove(node);
                // Defensive rather than load-bearing: a rule is only ever enrolled by the branch
                // below, which is also what put the graph edges there, so the two always come and go
                // together.
                builder.Withdraw(node);
                return;
            }

            builder.Graph.Set(node, references);
            builder.Enrol(new RuleParticipant(rule, _options));
        });
    }

    private IReadOnlyList<string> ReferencesOf(string? documentJson)
    {
        if (documentJson is null)
            return [];

        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(_options).Parse(documentJson, errors);
        // The document has already bound by the time this runs, so a parse failure is impossible.
        return document is null ? [] : DocumentReferences.From(document);
    }

    /// <summary>Adapts a rule to the rebind transaction, supplying it a serializer over the prospective source.</summary>
    private sealed class RuleParticipant(RuleBase rule, RuleSerializerOptions options) : IRebindable
    {
        public NodeId Node { get; } = NodeId.Rule(rule.Name);

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors) =>
            rule.PrepareRebind(new RuleSerializer(prospective, options), errors);
    }
}
