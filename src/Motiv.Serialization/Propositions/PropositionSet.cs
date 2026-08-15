namespace Motiv.Serialization;

/// <summary>
/// The authored propositions an application resolves alongside its compiled ones. Mirrors
/// <see cref="RuleSet"/>: validate, bind, then publish atomically, with optimistic version checks on
/// writes. Unlike a rule, a proposition is *referenceable*, so publishing one also rebinds everything
/// that references it — all of it, or none.
/// </summary>
public sealed class PropositionSet
{
    private readonly IPropositionStore _store;
    private readonly RuleSerializerOptions _options;
    private readonly Dictionary<string, PropositionModelBinding> _models = new(StringComparer.Ordinal);
    private bool _loaded;

    /// <summary>
    /// Creates a proposition set whose documents bind against the given registry, persisting to the
    /// given store.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="RuleSet(SpecRegistry, IRuleStore, RuleSerializerOptions)"/>, and opens a
    /// binding scope of its own over the registry. Rules that are to see these propositions must
    /// therefore be built from *this set* —
    /// <see cref="RuleSet(PropositionSet, IRuleStore, RuleSerializerOptions)"/> — not from the
    /// registry a second time; the registry refuses the latter rather than let the two drift apart
    /// unnoticed. The intended order is: construct, <see cref="AddModel{TModel}"/>,
    /// <see cref="Load"/>, then build the rule set, so a rule's default document may reference an
    /// authored proposition.
    /// </remarks>
    /// <param name="registry">The registry proposition documents resolve spec references against.</param>
    /// <param name="store">Where authored propositions persist.</param>
    /// <param name="options">Options forwarded to the document parser and binder, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> or <paramref name="store"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// A <see cref="RuleSet"/> was already built from <paramref name="registry"/>, so this set could
    /// only ever be invisible to it.
    /// </exception>
    public PropositionSet(SpecRegistry registry, IPropositionStore store, RuleSerializerOptions? options = null)
        : this(ScopeOver(registry, store), store, options)
    {
    }

    /// <summary>
    /// Opens the scope the public constructor chains through, having first checked the arguments
    /// that constructor would otherwise only reach afterwards. Claiming a registry mutates an object
    /// the caller owns and keeps, so a construction that is going to throw must not leave a claim
    /// behind — the caller would be left holding a registry marked as backing a proposition set that
    /// does not exist.
    /// </summary>
    private static BindingScope ScopeOver(SpecRegistry registry, IPropositionStore store)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));

        return BindingScope.For(registry, ScopeClaim.Propositions);
    }

    /// <summary>
    /// Creates a proposition set sharing an existing <see cref="BindingScope"/>, so its edits reach
    /// whatever else publishes under that scope.
    /// </summary>
    internal PropositionSet(BindingScope scope, IPropositionStore store, RuleSerializerOptions? options = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new RuleSerializerOptions();

        // Last, and only once everything a rebuild reads is assigned: Join publishes this set to the
        // scope, and a refresh arriving immediately afterwards would call straight into it.
        Scope.Join(this);
    }

    /// <summary>The coordinator this set publishes under, and what a paired <see cref="RuleSet"/> joins.</summary>
    internal BindingScope Scope { get; }

    /// <summary>
    /// Pins the current world for the duration of a decision, so several evaluations resolve against
    /// one published set. Dispose to release. Hosts using <c>MapMotivRules</c> get one per request
    /// automatically and need not call this.
    /// </summary>
    public DecisionSnapshot PinSnapshot() => new(Scope);

    /// <summary>
    /// Options forwarded to the document parser and binder. Reachable by <see cref="AuthoredProposition"/>,
    /// which parses its own document again when preparing a rebind.
    /// </summary>
    internal RuleSerializerOptions Options => _options;

    /// <summary>
    /// Registers a model type authored propositions may be written against, capturing
    /// <typeparamref name="TModel"/> behind a closure so no binding step needs reflection.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type.</typeparam>
    /// <param name="modelTypeId">The stable id clients pass as <c>modelType</c>.</param>
    /// <returns>This set, to allow chained registration.</returns>
    public PropositionSet AddModel<TModel>(string modelTypeId) =>
        Scope.Locked(() =>
        {
            _models[modelTypeId] = new PropositionModelBinding
            {
                Id = modelTypeId,
                ModelType = typeof(TModel),
                Bind = (source, name, description, document, isAsync, errors) =>
                {
                    if (isAsync)
                    {
                        var asyncSpec = AsyncRuleBinder.Bind<TModel>(document, source, errors);
                        return asyncSpec is null
                            ? null
                            : new SpecRegistryEntry(name, typeof(TModel), typeof(string), true, asyncSpec, description);
                    }

                    var spec = RuleBinder.Bind<TModel>(document, source, errors);
                    return spec is null
                        ? null
                        : new SpecRegistryEntry(name, typeof(TModel), typeof(string), false, spec, description);
                }
            };
            return this;
        });

    /// <summary>
    /// Every proposition in scope — compiled, overridden and authored — as one effective listing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads the authored half out of a generation rather than a dictionary of this set's own, which
    /// is what makes a listing consistent with what the evaluator resolves: a cascaded rebind lands in
    /// the generation and nowhere else, so a second copy could only ever go stale.
    /// </para>
    /// <para>
    /// <see cref="BindingScope.Active"/>, not <see cref="BindingScope.Current"/> — the read-only side
    /// of the split described in <see cref="BindingScope.Active"/>'s own remarks. This listing binds
    /// nothing and publishes nothing, so a pin cannot make it stale in any way that matters; what a
    /// pin does buy is that a request describing a world in its headers returns a body from that same
    /// world.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<PropositionEntry> Propositions =>
        Scope.Locked(() =>
        {
            var entries = new Dictionary<string, PropositionEntry>(StringComparer.Ordinal);

            foreach (var compiled in Scope.Registry.Entries)
                entries[compiled.Name] = ToEntry(compiled);

            foreach (var authored in Scope.Active.Authored.Values)
                entries[authored.Name] = ToEntry(authored);

            return (IReadOnlyCollection<PropositionEntry>)[.. entries.Values];
        });

    /// <summary>One proposition's listing, or null when the name is unknown.</summary>
    /// <remarks>Read-only, so <see cref="BindingScope.Active"/> — see <see cref="Propositions"/>.</remarks>
    public PropositionEntry? Find(string name) =>
        Scope.Locked<PropositionEntry?>(() =>
        {
            if (Scope.Active.Authored.TryGetValue(name, out var authored))
                return ToEntry(authored);

            return Scope.Registry.Find(name) is { } compiled ? ToEntry(compiled) : null;
        });

    /// <summary>The authored document behind a name, or null when the name has no authored document.</summary>
    /// <remarks>Read-only, so <see cref="BindingScope.Active"/> — see <see cref="Propositions"/>.</remarks>
    public string? DocumentJsonOf(string name) =>
        Scope.Locked(() =>
            Scope.Active.Authored.TryGetValue(name, out var authored) ? authored.DocumentJson : null);

    /// <summary>The nodes that reference the given proposition, transitively, in rebind order.</summary>
    /// <remarks>Read-only, so <see cref="BindingScope.Active"/> — see <see cref="Propositions"/>.</remarks>
    public IReadOnlyList<PropositionDependent> Dependents(string name) =>
        Scope.Locked(() => (IReadOnlyList<PropositionDependent>)
            [.. Scope.Active.Graph.DependentClosure(name)
                .Select(node => new PropositionDependent(node.Name, node.KindLabel))]);

    /// <summary>
    /// Authors a new proposition. A name already carrying an authored document is a conflict; a name
    /// carrying only a compiled spec is accepted and creates an override — which, because existing
    /// documents already reference that name, rebinds everything that does, transactionally, as
    /// <see cref="UpdateAsync"/> would.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="modelTypeId">A registered model-type id.</param>
    /// <param name="documentJson">The rule document defining the proposition.</param>
    /// <param name="description">An optional description.</param>
    /// <param name="cancellationToken">Cancels while waiting for the gate or the store.</param>
    /// <returns>
    /// The outcome, carrying the dependents that broke when an override is why it was rejected.
    /// Nothing is published or persisted unless it is <c>Created</c>.
    /// </returns>
    public Task<PropositionUpdateResult> CreateAsync(
        string name, string modelTypeId, string documentJson, string? description,
        CancellationToken cancellationToken = default) =>
        Scope.LockedAsync(
            () => CreateCoreAsync(name, modelTypeId, documentJson, description, cancellationToken),
            cancellationToken);

    /// <summary>
    /// <see cref="CreateAsync"/> without taking the outer scope gate, for a caller already holding it.
    /// Differs from <see cref="UpdateCoreAsync"/> only in what it prepares and what a success is
    /// called; the persist-and-commit choreography the two share — and the locking that choreography
    /// exists to get right — lives in <see cref="PersistAndCommitCoreAsync"/>.
    /// </summary>
    internal Task<PropositionUpdateResult> CreateCoreAsync(
        string name, string modelTypeId, string documentJson, string? description,
        CancellationToken cancellationToken) =>
        PersistAndCommitCoreAsync(
            () => PrepareCreateCascade(name, modelTypeId, documentJson, description),
            PropositionUpdateResult.Created,
            cancellationToken);

    /// <summary>
    /// The prepare half of <see cref="CreateCoreAsync"/>: a lone create, binding against a fresh
    /// single-use fork of the live world. Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    private WritePrepare PrepareCreateCascade(
        string name, string modelTypeId, string documentJson, string? description) =>
        // A lone write has no other envelope members to protect from a stale rebind, so the
        // exclusion set is empty.
        PrepareCreateCore(
            name, modelTypeId, documentJson, description,
            new ScopeGenerationBuilder(Scope.Registry, Scope.Current), []);

    /// <summary>
    /// The governed-envelope counterpart of <see cref="PrepareCreateCascade"/>: binds against
    /// <paramref name="prospective"/> rather than a fresh fork of the live world, so a proposition
    /// prepared earlier in the same envelope — not yet committed, since nothing commits until the
    /// whole envelope has persisted — is visible to this one. The caller owns <paramref name="prospective"/>
    /// across the whole envelope and folds every member into it in turn. Assumes
    /// <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    /// <param name="excluding">
    /// Every node the envelope also explicitly publishes or withdraws elsewhere — see
    /// <see cref="BindingScope.PrepareClosure"/>'s remarks for why this must never be omitted for a
    /// governed publish.
    /// </param>
    internal WritePrepare PrepareCreateCore(
        string name, string modelTypeId, string documentJson, string? description,
        ScopeGenerationBuilder prospective, HashSet<NodeId> excluding)
    {
        // The *live* world, deliberately, not prospective.Source's: a governed envelope that creates
        // two propositions must judge each one against what is actually published, exactly as it did
        // when this read came from a dictionary of the set's own.
        if (Scope.Current.Authored.ContainsKey(name))
            return WritePrepare.Rejected(PropositionUpdateResult.NameTaken());

        if (ValidateName(name) is { } nameError)
            return WritePrepare.Rejected(PropositionUpdateResult.Invalid([nameError]));

        var prepared = Prepare(name, modelTypeId, documentJson, description, prospective.Source);
        if (prepared.Entry is not { } entry)
            return WritePrepare.Rejected(PropositionUpdateResult.Invalid(prepared.Errors));

        // A brand-new name has no referrers, so its closure is empty and the cascade below is a
        // no-op. An *override* is the exception, and the reason a create cascades at all: it
        // lands on a name existing documents already reference, so publishing it changes what
        // they resolve exactly as an update would, on the same all-or-nothing terms.
        var authored = new AuthoredProposition(
            this, name, modelTypeId, documentJson, version: 1, description,
            bound: entry, quarantine: [], references: prepared.References);

        return PrepareCascadeInto(authored, prospective, excluding);
    }

    /// <summary>
    /// Replaces an authored proposition's document, rebinding everything that references it. Either
    /// the whole closure rebinds and the new document is published, or nothing moves at all.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="documentJson">The replacement document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="cancellationToken">Cancels while waiting for the gate or the store.</param>
    /// <returns>The outcome, carrying the dependents that broke when that is why it was rejected.</returns>
    public Task<PropositionUpdateResult> UpdateAsync(
        string name, string documentJson, int expectedVersion, CancellationToken cancellationToken = default) =>
        Scope.LockedAsync(
            () => UpdateCoreAsync(name, documentJson, expectedVersion, cancellationToken),
            cancellationToken);

    /// <summary><see cref="UpdateAsync"/> without taking the outer scope gate. See <see cref="CreateCoreAsync"/>.</summary>
    internal Task<PropositionUpdateResult> UpdateCoreAsync(
        string name, string documentJson, int expectedVersion, CancellationToken cancellationToken) =>
        PersistAndCommitCoreAsync(
            () => PrepareUpdateCascade(name, documentJson, expectedVersion),
            PropositionUpdateResult.Updated,
            cancellationToken);

    /// <summary>
    /// The prepare half of <see cref="UpdateCoreAsync"/>: a lone update, binding against a fresh
    /// single-use fork of the live world. Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    private WritePrepare PrepareUpdateCascade(string name, string documentJson, int expectedVersion) =>
        // See PrepareCreateCascade: a lone write's exclusion set is always empty.
        PrepareUpdateCore(
            name, documentJson, expectedVersion,
            new ScopeGenerationBuilder(Scope.Registry, Scope.Current), []);

    /// <summary>
    /// The governed-envelope counterpart of <see cref="PrepareUpdateCascade"/>. See
    /// <see cref="PrepareCreateCore"/>'s remarks — the same reasoning applies to a replacement.
    /// </summary>
    internal WritePrepare PrepareUpdateCore(
        string name, string documentJson, int expectedVersion,
        ScopeGenerationBuilder prospective, HashSet<NodeId> excluding)
    {
        if (!Scope.Current.Authored.TryGetValue(name, out var current))
            return WritePrepare.Rejected(PropositionUpdateResult.NotFound());

        if (current.Version != expectedVersion)
            return WritePrepare.Rejected(PropositionUpdateResult.VersionConflict(current.Version));

        var prepared = Prepare(
            name, current.ModelTypeId, documentJson, current.Description, prospective.Source);
        if (prepared.Entry is not { } entry)
            return WritePrepare.Rejected(PropositionUpdateResult.Invalid(prepared.Errors));

        var replacement = new AuthoredProposition(
            this, name, current.ModelTypeId, documentJson, current.Version + 1, current.Description,
            bound: entry, quarantine: [], references: prepared.References);

        return PrepareCascadeInto(replacement, prospective, excluding);
    }

    /// <summary>
    /// The middle of bind → persist → publish, for a caller already holding the outer gate — see
    /// <see cref="RuleSet.PersistAndCommitCoreAsync"/>, whose shape this mirrors. The store call is
    /// the last step that can fail; everything after it is a memory swap that cannot.
    /// </summary>
    /// <remarks>
    /// Holds the inner scope monitor around <paramref name="prepare"/> and around the commit, with
    /// the store round trip in between left unlocked — see the class remarks on
    /// <see cref="BindingScope"/> for why both tiers matter and why the store call must sit outside
    /// the monitor. Create and update share this method precisely because that shape is a
    /// correctness requirement rather than a convenience: one copy is one thing to audit.
    /// </remarks>
    /// <param name="prepare">Binds the closure and decides whether the write may proceed.</param>
    /// <param name="success">Names the outcome of a committed write, given its new version.</param>
    /// <param name="cancellationToken">Cancels the store round trip.</param>
    private async Task<PropositionUpdateResult> PersistAndCommitCoreAsync(
        Func<WritePrepare> prepare, Func<int, PropositionUpdateResult> success,
        CancellationToken cancellationToken)
    {
        var prepared = Scope.Locked(prepare);
        if (prepared.Failure is { } failure)
            return failure;

        await _store.WriteAsync(SaveBatchFor(prepared.Authored!), cancellationToken).ConfigureAwait(false);

        return Scope.Locked(() =>
            success(Scope.Mutate(builder => CommitPublishCore(prepared, builder))));
    }

    /// <summary>
    /// Prepares a cascading publish: binds the closure over a prospective world carrying
    /// <paramref name="authored"/>'s new definition, so a dependent is checked against what it
    /// *would* resolve rather than what it resolves today. Shared by <see cref="PrepareCreateCore"/>
    /// and <see cref="PrepareUpdateCore"/>, which differ only in how <paramref name="authored"/>
    /// itself was built. <paramref name="prospective"/> is a fresh single-use fork of the live world
    /// for a lone write, and the envelope's own cumulative successor for a governed publish. Assumes
    /// the scope lock is held.
    /// </summary>
    private WritePrepare PrepareCascadeInto(
        AuthoredProposition authored, ScopeGenerationBuilder prospective, HashSet<NodeId> excluding)
    {
        prospective.SetOverlayEntry(authored.Bound!);

        var commits = new List<IRebindCommit>();
        var broken = Scope.PrepareClosure(authored.Name, prospective, commits, excluding);

        return broken.Count > 0
            ? WritePrepare.Rejected(PropositionUpdateResult.BreaksDependents(broken))
            : WritePrepare.Ready(authored, commits);
    }

    /// <summary>The batch a publish of <paramref name="authored"/> writes: one save, nothing removed.</summary>
    private static PropositionBatch SaveBatchFor(AuthoredProposition authored) => PropositionBatch.Save(RowFor(authored));

    /// <summary>The stored row a publish of <paramref name="authored"/> writes — the save half of a governed envelope's batch.</summary>
    internal static StoredProposition RowFor(AuthoredProposition authored) =>
        new(authored.Name, authored.ModelTypeId, authored.DocumentJson, authored.Version, authored.Description);

    /// <summary>
    /// Applies a batch of prepared writes to the store as one round trip — the proposition half of a
    /// governed envelope's persist phase, mirroring <see cref="RuleSet.AppendCoreAsync"/>. Assumes the
    /// outer gate is held; commits nothing.
    /// </summary>
    internal Task WriteBatchCoreAsync(PropositionBatch batch, CancellationToken cancellationToken) =>
        _store.WriteAsync(batch, cancellationToken);

    /// <summary>
    /// Applies a create or update prepared earlier by <see cref="PrepareCreateCore"/> or
    /// <see cref="PrepareUpdateCore"/> — the publish half of a governed envelope's apply phase,
    /// mirroring <see cref="RuleSet.CommitCore"/>. Has no failure outcome: everything a caller can get
    /// wrong was already decided by the prepare, and the store write in between has already succeeded
    /// by the time this runs. Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    /// <param name="prepared">The prepared write, from the same overlay its closure was bound against.</param>
    /// <param name="builder">
    /// The successor being built. Taken rather than opened here so a governed envelope can pass one
    /// builder through every member it commits and publish the lot in a single swap.
    /// </param>
    /// <returns>The newly published version.</returns>
    internal static int CommitPublishCore(WritePrepare prepared, ScopeGenerationBuilder builder)
    {
        var authored = prepared.Authored!;
        CommitPublish(authored, builder);
        builder.Apply(prepared.Commits!);
        return authored.Version;
    }

    /// <summary>
    /// Applies a withdrawal prepared earlier by <see cref="PrepareWithdraw"/> or
    /// <see cref="PrepareWithdrawCore"/>. The withdrawal counterpart of <see cref="CommitPublishCore"/>,
    /// and infallible for the same reasons. Leaves no authored document behind, so there is no version
    /// to report. Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    internal static void CommitWithdrawCore(WritePrepare prepared, ScopeGenerationBuilder builder)
    {
        builder.Apply(prepared.Commits!);

        // After the closure rather than before, which the order otherwise implies matters: it does
        // not. A cascaded rebind writes only a dependent's own name, never current.Name, so the two
        // can never touch the same key — and neither is visible to anyone until this builder is
        // swapped in.
        var current = prepared.Authored!;
        builder.RemoveAuthored(current.Name);
        builder.RemoveOverlayEntry(current.Name);
        builder.Graph.Remove(current.Node);
        // Defensive rather than load-bearing: a proposition is only ever enrolled by CommitPublish,
        // which is also what put the graph edges above, so the two always come and go together.
        builder.Withdraw(current.Node);
    }

    /// <summary>
    /// Withdraws an authored document. When a compiled spec lies beneath the name this reverts to it
    /// — permitted even while referenced, because referrers keep resolving. When nothing lies beneath,
    /// this removes the proposition outright, which is refused while anything references it.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="cancellationToken">Cancels while waiting for the gate or the store.</param>
    /// <returns>The outcome.</returns>
    public Task<PropositionUpdateResult> WithdrawAsync(
        string name, int expectedVersion, CancellationToken cancellationToken = default) =>
        Scope.LockedAsync(
            () => WithdrawCoreAsync(name, expectedVersion, cancellationToken),
            cancellationToken);

    /// <summary><see cref="WithdrawAsync"/> without taking the outer scope gate. See <see cref="CreateCoreAsync"/>.</summary>
    internal async Task<PropositionUpdateResult> WithdrawCoreAsync(
        string name, int expectedVersion, CancellationToken cancellationToken)
    {
        var prepared = Scope.Locked(() => PrepareWithdraw(name, expectedVersion));
        if (prepared.Failure is { } failure)
            return failure;

        await _store.WriteAsync(PropositionBatch.Delete(name), cancellationToken).ConfigureAwait(false);

        return Scope.Locked(() =>
        {
            Scope.Mutate(builder => CommitWithdrawCore(prepared, builder));
            return PropositionUpdateResult.Removed();
        });
    }

    /// <summary>
    /// The prepare half of <see cref="WithdrawCoreAsync"/>: a lone withdrawal, so its
    /// <see cref="BindingScope.PrepareClosure"/> call excludes nothing — there is no envelope, and
    /// therefore no other member's own prepare that could need protecting from this one's closure
    /// walk. See <see cref="PrepareWithdrawCore"/> for the governed counterpart, which takes the
    /// envelope's exclusion set as a parameter instead. Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    private WritePrepare PrepareWithdraw(string name, int expectedVersion)
    {
        if (!Scope.Current.Authored.TryGetValue(name, out var current))
            return WritePrepare.Rejected(PropositionUpdateResult.NotFound());

        if (current.Version != expectedVersion)
            return WritePrepare.Rejected(PropositionUpdateResult.VersionConflict(current.Version));

        var compiled = Scope.Registry.Find(name);
        var commits = new List<IRebindCommit>();

        if (compiled is null)
        {
            // Removal would leave referrers pointing at nothing, so direct referrers block it.
            var referrers = Scope.Current.Graph.Referrers(name);
            if (referrers.Count > 0)
                return WritePrepare.Rejected(
                    PropositionUpdateResult.Referenced([.. referrers.Select(node => node.Name)]));
        }
        else
        {
            // Reverting changes what referrers resolve, so it takes the same transactional check
            // as any other edit — the compiled spec may not satisfy every dependent.
            var prospective = new ScopeGenerationBuilder(Scope.Registry, Scope.Current);
            prospective.RemoveOverlayEntry(name);

            var broken = Scope.PrepareClosure(name, prospective, commits, []);
            if (broken.Count > 0)
                return WritePrepare.Rejected(PropositionUpdateResult.BreaksDependents(broken));
        }

        return WritePrepare.Ready(current, commits);
    }

    /// <summary>
    /// The governed-envelope counterpart of <see cref="PrepareWithdraw"/>: binds the compiled-default
    /// closure check (when one lies beneath) against <paramref name="prospective"/> rather than a
    /// fresh copy of the live overlay, so it reflects every proposition and rule already prepared
    /// earlier in the same envelope. Assumes <see cref="BindingScope"/>'s inner monitor is
    /// held.
    /// </summary>
    /// <remarks>
    /// Deliberately does <em>not</em> repeat the direct-referrer check <see cref="PrepareWithdraw"/>
    /// runs when nothing is compiled beneath the name. A governed publish's <c>Validate</c> pass
    /// already ran that exact check — accounting for referrers this same envelope redirects away, via
    /// its own <c>rebound</c>/<c>withdrawn</c> bookkeeping — before this prepare phase is ever reached,
    /// under the same exclusive gate. Re-deriving it here would either duplicate that bookkeeping or
    /// regress to the live-graph-only view <c>PrepareWithdraw</c> uses, which is exactly what would
    /// wrongly refuse a withdrawal whose only referrer is redirected earlier in the same envelope.
    /// </remarks>
    internal WritePrepare PrepareWithdrawCore(
        string name, int expectedVersion, ScopeGenerationBuilder prospective, HashSet<NodeId> excluding)
    {
        if (!Scope.Current.Authored.TryGetValue(name, out var current))
            return WritePrepare.Rejected(PropositionUpdateResult.NotFound());

        if (current.Version != expectedVersion)
            return WritePrepare.Rejected(PropositionUpdateResult.VersionConflict(current.Version));

        if (Scope.Registry.Find(name) is null)
        {
            // No direct-referrer check here — see the remarks — but the name still leaves the
            // envelope's cumulative overlay, exactly as Validate's phase C does for this same arm:
            // both passes must see the same sequence of worlds, and a later envelope member (a
            // proposition created after this withdrawal, say) must not resolve a name this envelope
            // is also removing.
            prospective.RemoveOverlayEntry(name);
            return WritePrepare.Ready(current, []);
        }

        // Reverting to the compiled default changes what referrers resolve, so — as in
        // PrepareWithdraw — the dependent closure is re-checked, here against the envelope's own
        // cumulative prospective overlay. excluding matters here exactly as it does for a publish's
        // own cascade: a dependent this envelope is *also* explicitly publishing or withdrawing must
        // not be rebound from its stale, pre-envelope definition — see PrepareClosure's remarks.
        prospective.RemoveOverlayEntry(name);
        var commits = new List<IRebindCommit>();
        var broken = Scope.PrepareClosure(name, prospective, commits, excluding);

        return broken.Count > 0
            ? WritePrepare.Rejected(PropositionUpdateResult.BreaksDependents(broken))
            : WritePrepare.Ready(current, commits);
    }

    /// <summary>
    /// One name's authored state, read without taking the scope lock, for a caller already holding
    /// it. A name with no authored document reports <c>Exists: false</c> and version 0 even when a
    /// compiled spec resolves beneath it — authoring over a compiled spec is a creation.
    /// </summary>
    internal (bool Exists, int Version, string? ModelTypeId, string? Description) AuthoredStateCore(string name) =>
        Scope.Current.Authored.TryGetValue(name, out var authored)
            ? (true, authored.Version, authored.ModelTypeId, authored.Description)
            : (false, 0, null, null);

    /// <summary>
    /// Parses, cycle-checks and binds a proposed document against <paramref name="source"/> without
    /// publishing anything — the proposition half of a governed publish's validate-everything-first
    /// phase. Passing a prospective source lets one envelope member resolve against another that is
    /// not live yet. Assumes <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    /// <returns>
    /// The bound entry to fold into the prospective overlay and the names it resolves, or the errors
    /// that stopped it.
    /// </returns>
    internal (SpecRegistryEntry? Entry, IReadOnlyList<string> References, IReadOnlyList<RuleError> Errors) PrepareCore(
        string name, string modelTypeId, string documentJson, string? description, ISpecSource source)
    {
        if (ValidateName(name) is { } nameError)
            return (null, [], [nameError]);

        var prepared = Prepare(name, modelTypeId, documentJson, description, source);
        return (prepared.Entry, prepared.References, prepared.Errors);
    }

    /// <summary>
    /// Reads every persisted proposition and binds it, in dependency order. A document that fails to
    /// bind — or that depends on one which did — is *quarantined* rather than fatal: it is excluded
    /// from the effective set with its errors recorded, its document retained for repair, and any
    /// compiled spec beneath the name left to resolve in its place.
    /// </summary>
    /// <remarks>
    /// This is deliberately asymmetric with <see cref="RuleSet.Add"/>, which fails fast. A compiled
    /// default failing to bind is a developer error and should stop startup. A persisted document
    /// failing to bind is an operational reality — a redeploy renames a C# spec a saved proposition
    /// referenced — and refusing to boot would turn a stale row into an outage. Call once, before
    /// rules are added, so a rule's default document may reference an authored proposition.
    /// Repairing a quarantined proposition via <see cref="UpdateAsync"/> does not retroactively
    /// un-quarantine anything that depended on it while it was broken — quarantine leaves no graph
    /// edges, so those dependents are not tracked as needing a rebind and stay quarantined until they
    /// are themselves updated.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Load has already been called on this set.</exception>
    public void Load() =>
        Scope.Locked(() =>
        {
            // "Call once" is a precondition, not advice. A second pass cannot re-run cleanly: a row
            // that binds the first time and quarantines the second has already had its overlay entry
            // and its graph edges written, and the quarantine path clears neither — leaving the
            // catalog reporting it broken while the evaluator still resolves the stale binding. A
            // refresh would have to be a whole rebuild, so refuse rather than half-do it.
            if (_loaded)
                throw new InvalidOperationException(
                    "Load has already been called on this PropositionSet. It reads the store once, " +
                    "at startup, before rules are added; it is not a refresh.");

            // Set only once the store has actually been read. Reading is the one step here that can
            // throw rather than quarantine, and it mutates nothing — so a store that was briefly
            // unreachable leaves the set in its pre-load state and genuinely may be loaded again.
            var candidates = CandidatesFrom(_store.Load());
            _loaded = true;

            var ordered = OrderAndRecordCycles(candidates);

            // An empty store publishes nothing at all. A successor identical to its predecessor would
            // still move the write stamp, and that stamp is a refresh's compare-and-set signal — see
            // BindingScope.WriteStamp — so a swap with nothing in it is not free.
            if (ordered.Count == 0)
                return;

            // One builder for the whole store, not one world per row. Each row binds against the
            // builder's own prospective source, so a proposition still resolves the ones it references
            // — they were folded in earlier in this same dependency-ordered pass — while startup
            // publishes a single generation instead of forking one per row.
            Scope.Mutate(builder =>
            {
                foreach (var name in ordered)
                    LoadOne(candidates[name], builder);
            });
        });

    /// <summary>
    /// Rebuilds this replica's world from the stores, if either has moved since it was last built.
    /// </summary>
    /// <remarks>
    /// The whole world is rebuilt, not a part of it: a row that binds on one pass and quarantines on
    /// the next has already written its overlay entry and graph edges, and the quarantine path clears
    /// neither — which is why <see cref="Load"/> refuses to run twice and this exists instead. A scope
    /// shared with a <see cref="RuleSet"/> rebuilds both halves whichever set you call.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the store reads.</param>
    /// <returns>What the refresh did, and anything that would not bind.</returns>
    public Task<RefreshReport> RefreshAsync(CancellationToken cancellationToken = default) =>
        Scope.RefreshAsync(cancellationToken);

    /// <summary>Where the proposition store stands. One scalar; polled on a timer.</summary>
    internal Task<long> StoreGenerationAsync(CancellationToken cancellationToken) =>
        _store.GetGenerationAsync(cancellationToken);

    /// <summary>
    /// Rebuilds the authored layer into <paramref name="builder"/> from the store. Mirrors
    /// <see cref="Load"/> step for step — read rows, order by dependency, quarantine cycles, bind —
    /// with one difference: a row that will not bind is a <em>regression</em> when the world being
    /// served has it bound, and merely carried when it does not.
    /// </summary>
    /// <param name="builder">The successor being built. Nobody else can see it, so nothing is locked.</param>
    /// <param name="current">The world being served, which decides which failures are regressions.</param>
    /// <param name="regressions">Filled with every failure that must abort the refresh.</param>
    /// <param name="quarantined">Filled with every failure carried forward instead.</param>
    /// <param name="cancellationToken">Cancels the store read.</param>
    internal async Task RebuildIntoAsync(
        ScopeGenerationBuilder builder, ScopeGeneration current,
        List<RefreshFailure> regressions, List<RefreshFailure> quarantined,
        CancellationToken cancellationToken)
    {
        var rows = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);

        // Everything below is CPU over a builder nobody else can see, so no lock is held or needed.
        var candidates = CandidatesFrom(rows);

        foreach (var name in OrderAndRecordCycles(candidates))
            RebuildOne(candidates[name], builder, current, regressions, quarantined);
    }

    /// <summary>
    /// <see cref="LoadOne"/> for a refresh: binds one stored proposition into <paramref name="builder"/>,
    /// and where <see cref="LoadOne"/> would simply quarantine a row that will not bind, decides first
    /// whether losing it would take a live binding away.
    /// </summary>
    private void RebuildOne(
        LoadCandidate candidate, ScopeGenerationBuilder builder, ScopeGeneration current,
        List<RefreshFailure> regressions, List<RefreshFailure> quarantined)
    {
        var stored = candidate.Stored;
        var authored = Unbound(candidate);

        // Carried forward rather than started fresh, and only attempted when empty — exactly as
        // LoadOne does, and for the same reason: a name failure, a parse failure or a cycle already
        // rules out binding, and an attempt on top of one could only succeed by resolving through a
        // name that is itself unresolvable.
        var errors = new List<RuleError>(candidate.Errors);

        if (errors.Count == 0 && authored.Rebind(builder.Source, errors) is { } rebound)
        {
            CommitPublish(rebound, builder);
            return;
        }

        var failure = new RefreshFailure(stored.Name, authored.Node.KindLabel, errors);

        // Bound in the world being served? Then applying the rebuild would take a working, approved
        // proposition away, and every dependent that resolves through it with it. Refuse.
        if (current.Authored.TryGetValue(stored.Name, out var live) && live.Bound is not null)
        {
            regressions.Add(failure);
            return;
        }

        // Carried, with the same SetAuthored both of LoadOne's quarantine arms make — see there for
        // why a world that cannot resolve a proposition must still list it.
        builder.SetAuthored(authored.WithQuarantine(errors));
        quarantined.Add(failure);
    }

    /// <summary>
    /// Orders candidates so each follows what it references, and records a cycle on every member of
    /// one. A hand-edited store can contain a reference cycle that Create/Update would have rejected
    /// outright — nothing on this path goes through <see cref="DependencyGraph.FindCycle"/> — so every
    /// member is quarantined with the real reason instead of being bound.
    /// </summary>
    private static IReadOnlyList<string> OrderAndRecordCycles(Dictionary<string, LoadCandidate> candidates)
    {
        var cycles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var ordered = OrderByDependency(candidates, cycles);

        foreach (var pair in cycles)
        {
            candidates[pair.Key].Errors.Add(new RuleError("$", RuleErrorCode.CycleDetected,
                $"the stored proposition '{pair.Key}' cannot be bound: {string.Join(" → ", pair.Value)} " +
                "forms a reference cycle"));
        }

        return ordered;
    }

    /// <summary>
    /// Turns stored rows into candidates keyed by name, parsing each document up front purely to
    /// order the binding. Name and parse failures are carried forward on the candidate rather than
    /// thrown, so the document is still listed, quarantined, rather than silently dropped.
    /// </summary>
    /// <remarks>
    /// Takes the rows rather than reading them, so <see cref="Load"/> (synchronous, at startup) and
    /// <see cref="RebuildIntoAsync"/> (asynchronous, on a refresh) share this one copy. The two differ
    /// only in how they got the rows and in what they do with a row that will not bind; everything
    /// between those two ends must not be able to drift.
    /// </remarks>
    /// <param name="rows">
    /// The store's rows. Nullable throughout — a store is a dumb sink, so a hand-edited or
    /// null-serialized one can hand back a null list as readily as a null row;
    /// <c>Deserialize&lt;List&lt;StoredProposition&gt;&gt;("[null]")</c> yields exactly that. Neither
    /// may be fatal: quarantine exists so a bad row costs its own row.
    /// </param>
    private Dictionary<string, LoadCandidate> CandidatesFrom(IReadOnlyList<StoredProposition>? rows)
    {
        var candidates = new Dictionary<string, LoadCandidate>(StringComparer.Ordinal);

        foreach (var proposition in rows ?? [])
        {
            // A quarantine entry is keyed by name, so a row with no usable name has nowhere to be
            // recorded and skipping it is the only non-fatal option. Every other malformed shape
            // carries a name and is quarantined instead, staying visible for repair.
            if (proposition?.Name is null)
                continue;

            var errors = new List<RuleError>();
            if (ValidateName(proposition.Name) is { } nameError)
                errors.Add(nameError);

            var document = SafeParse(proposition.DocumentJson, errors);
            candidates[proposition.Name] = new LoadCandidate(
                proposition, document is null ? [] : DocumentReferences.From(document), errors);
        }

        return candidates;
    }

    /// <summary>
    /// The stored row as an authored proposition that has not bound yet — what both
    /// <see cref="LoadOne"/> and <see cref="RebuildOne"/> start from, and what each either rebinds or
    /// quarantines.
    /// </summary>
    /// <remarks>
    /// Shared rather than written out twice for the same reason <see cref="CandidatesFrom"/> is: the
    /// two paths must differ only in what they do with a row that will not bind, and a nine-argument
    /// constructor copied into both is exactly where that promise would quietly stop holding.
    /// </remarks>
    private AuthoredProposition Unbound(LoadCandidate candidate)
    {
        var stored = candidate.Stored;
        return new AuthoredProposition(
            this, stored.Name, stored.ModelType, stored.DocumentJson, stored.Version, stored.Description,
            bound: null, quarantine: [], references: candidate.References);
    }

    /// <summary>
    /// Binds one stored proposition into <paramref name="builder"/>, publishing it or quarantining it.
    /// Writes nothing live: the caller swaps the whole load in as one generation.
    /// </summary>
    /// <remarks>
    /// <strong>Both quarantine arms must call <see cref="ScopeGenerationBuilder.SetAuthored"/>, and it
    /// is easy to think they need not.</strong> A quarantined row is the one kind of authored
    /// proposition that never goes through <see cref="CommitPublish"/> — it has no binding, so it gets
    /// no overlay entry, no graph edge and no enrolment, and every instinct says a world it cannot be
    /// resolved through should not carry it. But the generation's authored map is also what the
    /// catalog lists, and a quarantined document is precisely the one an operator needs to *see* in
    /// order to repair it. Drop these two writes and a broken stored row vanishes from
    /// <see cref="Find"/>, <see cref="Propositions"/>, <see cref="DocumentJsonOf"/> and
    /// <see cref="AuthoredStateCore"/> — silently, with every test still green, because nothing else
    /// in the load path notices. This was a live trap while the authored map was a field of this set:
    /// the arms below wrote to that field only, so moving the read path into the generation without
    /// moving them would have caused exactly that disappearance.
    /// </remarks>
    private void LoadOne(LoadCandidate candidate, ScopeGenerationBuilder builder)
    {
        var authored = Unbound(candidate);

        if (candidate.Errors.Count > 0)
        {
            // A name failure, a parse failure or a cycle already rules out binding — attempting it
            // anyway could only succeed by resolving through a name that is itself unresolvable or
            // by binding on top of an unresolved cyclic reference, neither of which is a real bind.
            builder.SetAuthored(authored.WithQuarantine(candidate.Errors));
            return;
        }

        var errors = new List<RuleError>();
        var rebound = authored.Rebind(builder.Source, errors);

        if (rebound is null)
        {
            // Quarantined: no overlay entry and no graph edges, so nothing resolves *to* it and
            // nothing is rebound *because* of it. Any compiled spec under the name still resolves.
            // It is still recorded as authored, so the catalog can list it and an operator can repair
            // the document it came from.
            builder.SetAuthored(authored.WithQuarantine(errors));
            return;
        }

        // The publish path itself rather than a copy of its steps, and missing only the store write
        // — which CommitPublish does not do either: this row came *from* the store, so saving it
        // back would be a no-op at best. Rebind is what makes that reuse possible; PrepareRebind
        // would seal the replacement inside a commit only a ScopeGenerationBuilder can open.
        CommitPublish(rebound, builder);
    }

    /// <summary>Parses without letting malformed JSON escape — a hand-edited store must not stop startup.</summary>
    private RuleDocument? SafeParse(string documentJson, List<RuleError> errors)
    {
        try
        {
            return new RuleDocumentParser(_options).Parse(documentJson, errors);
        }
        // JsonException from malformed JSON text is already caught and reported inside Parse itself.
        // This catch exists for what Parse does *not* guard: most notably a `null` DocumentJson —
        // a hand-edited or serialized-with-nulls store can produce one even though the property is
        // typed non-nullable — which reaches JsonDocument.Parse and throws ArgumentNullException,
        // an exception Parse's own catch does not cover. Without this catch, that null would crash
        // startup, defeating the entire point of Load.
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode,
                $"the stored document could not be read: {exception.Message}"));
            return null;
        }
    }

    /// <summary>
    /// Orders stored names so a proposition follows every stored proposition it references, and
    /// records every name that participates in a reference cycle in <paramref name="cycles"/>,
    /// keyed by name, with the full cycle path as its value. Names outside the store (compiled
    /// specs, or references that no longer resolve) are simply not edges.
    /// </summary>
    /// <remarks>
    /// Cycle membership does not exclude a name from the returned order — a cyclic name is going to
    /// be quarantined by the caller regardless of where it falls, so its position is irrelevant, and
    /// leaving it in keeps this method a single pass instead of two.
    /// </remarks>
    private static IReadOnlyList<string> OrderByDependency(
        IReadOnlyDictionary<string, LoadCandidate> candidates,
        Dictionary<string, IReadOnlyList<string>> cycles)
    {
        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        var onPath = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in candidates.Keys)
            Visit(name);

        return ordered;

        void Visit(string name)
        {
            if (visited.Contains(name))
                return;

            if (!onPath.Add(name))
            {
                // A back edge to a node still on the current path is a genuine cycle: extract it from
                // the path so every member can be quarantined with the real reason, rather than just
                // refusing to recurse and letting the incidental UnknownSpec from an unresolved
                // forward reference stand in for it.
                var start = path.IndexOf(name);
                var cycle = (IReadOnlyList<string>)[.. path.Skip(start), name];
                foreach (var member in cycle.Distinct())
                    cycles[member] = cycle;
                return;
            }

            path.Add(name);
            foreach (var reference in candidates[name].References)
            {
                if (candidates.ContainsKey(reference))
                    Visit(reference);
            }
            path.RemoveAt(path.Count - 1);
            onPath.Remove(name);

            visited.Add(name);
            ordered.Add(name);
        }
    }

    /// <summary>
    /// Validates the name against the registry's own grammar rather than a second copy of it — the
    /// two must agree exactly, because a document references the authored name the same way it
    /// references a compiled one.
    /// </summary>
    private static RuleError? ValidateName(string name) =>
        SpecRegistry.IsValidName(name)
            ? null
            : new RuleError("$.name", RuleErrorCode.InvalidSpecName,
                $"the name '{name}' is not a valid spec reference: each dot-separated segment must " +
                "start with an ASCII letter and contain only ASCII letters, digits, '-' or '_'");

    /// <summary>
    /// The binder registered for a model-type id, or null once the mismatch has been recorded. The
    /// id is typed nullable because a stored row really can carry one — the property that holds it
    /// is non-nullable, but a hand-edited or null-serialized store is not bound by that — and an
    /// unusable model type is a quarantine reason, never a reason to throw and fail startup.
    /// </summary>
    internal PropositionModelBinding? ResolveModel(string? modelTypeId, List<RuleError> errors)
    {
        if (modelTypeId is not null && _models.TryGetValue(modelTypeId, out var model))
            return model;

        errors.Add(new RuleError("$.modelType", RuleErrorCode.ModelTypeMismatch,
            $"model type '{modelTypeId}' is not registered for propositions"));
        return null;
    }

    /// <summary>The registered id for a CLR model type, or its type name when no id is registered.</summary>
    private string ResolveModelId(Type modelType) =>
        _models.Values.FirstOrDefault(model => model.ModelType == modelType)?.Id ?? modelType.Name;

    /// <summary>
    /// Whether a document referencing these names has to bind asynchronously. Asyncness is derived:
    /// an entry's own IsAsync already accounts for anything it references, so consulting the direct
    /// references is enough to know how the document must bind.
    /// </summary>
    internal static bool BindsAsync(ISpecSource source, IReadOnlyList<string> references) =>
        references.Any(reference => source.Find(reference) is { IsAsync: true });

    /// <summary>Parses, cycle-checks, and binds a document without publishing anything.</summary>
    /// <param name="source">
    /// Where references resolve, or null for the live source. A governed publish passes a
    /// prospective source so an envelope member can bind against another member that is not live yet.
    /// </param>
    private Prepared Prepare(
        string name, string modelTypeId, string documentJson, string? description, ISpecSource? source = null)
    {
        var against = source ?? Scope.Source;
        var errors = new List<RuleError>();

        var model = ResolveModel(modelTypeId, errors);
        if (model is null)
            return new Prepared(null, [], errors);

        var document = new RuleDocumentParser(_options).Parse(documentJson, errors);
        if (document is null || errors.Count > 0)
            return new Prepared(null, [], errors);

        var references = DocumentReferences.From(document);

        if (Scope.Current.Graph.FindCycle(name, references) is { } cycle)
        {
            errors.Add(new RuleError("$", RuleErrorCode.CycleDetected,
                $"publishing '{name}' would create a reference cycle: {string.Join(" → ", cycle)}"));
            return new Prepared(null, references, errors);
        }

        var isAsync = BindsAsync(against, references);
        var entry = model.Bind(against, name, description, document, isAsync, errors);
        return new Prepared(entry, references, errors);
    }

    /// <summary>
    /// The infallible half of a publish, for a caller that has already persisted the document — the
    /// counterpart of <see cref="RuleSet.CommitCore"/>. Folds the authored proposition into the
    /// successor's authored table, overlay, graph and participant table — all into one builder, so all
    /// four facts go live in the single swap that publishes it. Assumes <see cref="BindingScope"/>'s
    /// inner monitor is held: the caller's fork-and-swap relies on no second writer being in flight.
    /// </summary>
    internal static void CommitPublish(AuthoredProposition authored, ScopeGenerationBuilder builder)
    {
        builder.SetAuthored(authored);
        builder.SetOverlayEntry(authored.Bound!);
        builder.Graph.Set(authored.Node, authored.References);
        builder.Enrol(authored);
    }

    private PropositionEntry ToEntry(AuthoredProposition authored)
    {
        var compiled = Scope.Registry.Find(authored.Name);
        var origin = compiled is null ? PropositionOrigin.Authored : PropositionOrigin.Overridden;

        // A quarantined proposition has no binding of its own, so its shape is reported from the
        // compiled spec still resolving beneath it when there is one.
        var effective = authored.Bound ?? compiled;

        return new PropositionEntry(
            authored.Name,
            authored.ModelTypeId,
            effective?.MetadataType.Name ?? nameof(String),
            effective?.IsAsync ?? false,
            origin,
            authored.Version,
            authored.Description,
            authored.Quarantine);
    }

    /// <summary>
    /// A compiled spec with nothing authored over it: no authored version, nothing quarantined.
    /// </summary>
    private PropositionEntry ToEntry(SpecRegistryEntry entry) =>
        new(entry.Name, ResolveModelId(entry.ModelType), entry.MetadataType.Name, entry.IsAsync,
            PropositionOrigin.Compiled, Version: 0, entry.Description, []);

    /// <summary>The outcome of a prepare: the bound entry, its edges, and any errors.</summary>
    private readonly record struct Prepared(
        SpecRegistryEntry? Entry, IReadOnlyList<string> References, List<RuleError> Errors);

    /// <summary>
    /// One stored proposition on its way through <see cref="Load"/>: the row itself, the edges read
    /// from its document, and the reasons found so far to quarantine it rather than bind it. Kept as
    /// one record rather than three name-keyed dictionaries so the three can never disagree about a
    /// name, and so the cycle pass has an error list to append to unconditionally.
    /// </summary>
    private sealed record LoadCandidate(
        StoredProposition Stored, IReadOnlyList<string> References, List<RuleError> Errors);

    /// <summary>
    /// The outcome of preparing a write — create, update or withdraw: either what a caller must
    /// persist and then commit, or why it failed, in which case nothing is persisted or committed.
    /// Exactly one of <see cref="Authored"/>/<see cref="Commits"/> or <see cref="Failure"/> is
    /// non-null. The proposition half of <see cref="RulePrepareResult"/>.
    /// </summary>
    /// <param name="Authored">
    /// The proposition the write turns on: the definition to publish for a create or an update, the
    /// one to remove for a withdraw.
    /// </param>
    /// <param name="Commits">The dependent rebinds that go live with it.</param>
    /// <param name="Failure">Why the write was refused, or null when it may proceed.</param>
    /// <remarks>
    /// Internal, not private: a governed envelope (<see cref="ChangeRequestSet"/>) prepares every
    /// proposition edit up front via <see cref="PrepareCreateCore"/>/<see cref="PrepareUpdateCore"/>/
    /// <see cref="PrepareWithdrawCore"/>, and needs this shape to carry each one through the persist
    /// phase to <see cref="CommitPublishCore"/>/<see cref="CommitWithdrawCore"/>.
    /// </remarks>
    internal readonly record struct WritePrepare(
        AuthoredProposition? Authored, List<IRebindCommit>? Commits, PropositionUpdateResult? Failure)
    {
        /// <summary>A write that may proceed to the store.</summary>
        public static WritePrepare Ready(AuthoredProposition authored, List<IRebindCommit> commits) =>
            new(authored, commits, null);

        /// <summary>A write refused before anything was persisted.</summary>
        public static WritePrepare Rejected(PropositionUpdateResult failure) => new(null, null, failure);
    }
}
