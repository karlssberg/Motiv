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
    private readonly Dictionary<string, Authored> _authored = new(StringComparer.Ordinal);
    private bool _loaded;

    /// <summary>
    /// Creates a proposition set whose documents bind against the given registry, persisting to the
    /// given store.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="RuleSet(SpecRegistry, RuleSerializerOptions)"/>, and opens a binding scope
    /// of its own over the registry. Rules that are to see these propositions must therefore be built
    /// from *this set* — <see cref="RuleSet(PropositionSet, RuleSerializerOptions)"/> — not from the
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
    }

    /// <summary>The coordinator this set publishes under, and what a paired <see cref="RuleSet"/> joins.</summary>
    internal BindingScope Scope { get; }

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
    public IReadOnlyCollection<PropositionEntry> Propositions =>
        Scope.Locked(() =>
        {
            var entries = new Dictionary<string, PropositionEntry>(StringComparer.Ordinal);

            foreach (var compiled in Scope.Registry.Entries)
                entries[compiled.Name] = ToEntry(compiled);

            foreach (var authored in _authored.Values)
                entries[authored.Name] = ToEntry(authored);

            return (IReadOnlyCollection<PropositionEntry>)[.. entries.Values];
        });

    /// <summary>One proposition's listing, or null when the name is unknown.</summary>
    public PropositionEntry? Find(string name) =>
        Scope.Locked<PropositionEntry?>(() =>
        {
            if (_authored.TryGetValue(name, out var authored))
                return ToEntry(authored);

            return Scope.Registry.Find(name) is { } compiled ? ToEntry(compiled) : null;
        });

    /// <summary>The authored document behind a name, or null when the name has no authored document.</summary>
    public string? DocumentJsonOf(string name) =>
        Scope.Locked(() => _authored.TryGetValue(name, out var authored) ? authored.DocumentJson : null);

    /// <summary>The nodes that reference the given proposition, transitively, in rebind order.</summary>
    public IReadOnlyList<PropositionDependent> Dependents(string name) =>
        Scope.Locked(() => (IReadOnlyList<PropositionDependent>)
            [.. Scope.Graph.DependentClosure(name)
                .Select(node => new PropositionDependent(node.Name, node.KindLabel))]);

    /// <summary>
    /// Authors a new proposition. A name already carrying an authored document is a conflict; a name
    /// carrying only a compiled spec is accepted and creates an override — which, because existing
    /// documents already reference that name, rebinds everything that does, transactionally, as
    /// <see cref="Update"/> would.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="modelTypeId">A registered model-type id.</param>
    /// <param name="documentJson">The rule document defining the proposition.</param>
    /// <param name="description">An optional description.</param>
    /// <returns>
    /// The outcome, carrying the dependents that broke when an override is why it was rejected.
    /// Nothing is published or persisted unless it is <c>Created</c>.
    /// </returns>
    public PropositionUpdateResult Create(
        string name, string modelTypeId, string documentJson, string? description) =>
        Scope.Locked(() => CreateCore(name, modelTypeId, documentJson, description));

    /// <summary>
    /// <see cref="Create"/> without taking the scope lock, for a caller already holding it. See
    /// <see cref="RuleSet.UpdateCore"/> for why the split exists — a governed publish takes the
    /// lock once so that a whole envelope of edits is one atomic step.
    /// </summary>
    internal PropositionUpdateResult CreateCore(
        string name, string modelTypeId, string documentJson, string? description)
    {
        if (_authored.ContainsKey(name))
            return PropositionUpdateResult.NameTaken();

        if (ValidateName(name) is { } nameError)
            return PropositionUpdateResult.Invalid([nameError]);

        var prepared = Prepare(name, modelTypeId, documentJson, description);
        if (prepared.Entry is not { } entry)
            return PropositionUpdateResult.Invalid(prepared.Errors);

        // A brand-new name has no referrers, so its closure is empty and the cascade below is a
        // no-op. An *override* is the exception, and the reason a create cascades at all: it
        // lands on a name existing documents already reference, so publishing it changes what
        // they resolve exactly as an update would, on the same all-or-nothing terms.
        var authored = new Authored(this, name, modelTypeId, documentJson, version: 1, description)
        {
            Bound = entry,
            References = prepared.References
        };

        var broken = PublishWithCascade(authored);
        return broken.Count > 0
            ? PropositionUpdateResult.BreaksDependents(broken)
            : PropositionUpdateResult.Created(authored.Version);
    }

    /// <summary>
    /// Replaces an authored proposition's document, rebinding everything that references it. Either
    /// the whole closure rebinds and the new document is published, or nothing moves at all.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="documentJson">The replacement document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome, carrying the dependents that broke when that is why it was rejected.</returns>
    public PropositionUpdateResult Update(string name, string documentJson, int expectedVersion) =>
        Scope.Locked(() => UpdateCore(name, documentJson, expectedVersion));

    /// <summary><see cref="Update"/> without taking the scope lock. See <see cref="CreateCore"/>.</summary>
    internal PropositionUpdateResult UpdateCore(string name, string documentJson, int expectedVersion)
    {
        if (!_authored.TryGetValue(name, out var current))
            return PropositionUpdateResult.NotFound();

        if (current.Version != expectedVersion)
            return PropositionUpdateResult.VersionConflict(current.Version);

        var prepared = Prepare(name, current.ModelTypeId, documentJson, current.Description);
        if (prepared.Entry is not { } entry)
            return PropositionUpdateResult.Invalid(prepared.Errors);

        var replacement = new Authored(
            this, name, current.ModelTypeId, documentJson, current.Version + 1, current.Description)
        {
            Bound = entry,
            References = prepared.References
        };

        var broken = PublishWithCascade(replacement);
        return broken.Count > 0
            ? PropositionUpdateResult.BreaksDependents(broken)
            : PropositionUpdateResult.Updated(replacement.Version);
    }

    /// <summary>
    /// Withdraws an authored document. When a compiled spec lies beneath the name this reverts to it
    /// — permitted even while referenced, because referrers keep resolving. When nothing lies beneath,
    /// this removes the proposition outright, which is refused while anything references it.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome.</returns>
    public PropositionUpdateResult Withdraw(string name, int expectedVersion) =>
        Scope.Locked(() => WithdrawCore(name, expectedVersion));

    /// <summary><see cref="Withdraw"/> without taking the scope lock. See <see cref="CreateCore"/>.</summary>
    internal PropositionUpdateResult WithdrawCore(string name, int expectedVersion)
    {
        if (!_authored.TryGetValue(name, out var current))
            return PropositionUpdateResult.NotFound();

        if (current.Version != expectedVersion)
            return PropositionUpdateResult.VersionConflict(current.Version);

        var compiled = Scope.Registry.Find(name);
        var commits = new List<IRebindCommit>();

        if (compiled is null)
        {
            // Removal would leave referrers pointing at nothing, so direct referrers block it.
            var referrers = Scope.Graph.Referrers(name);
            if (referrers.Count > 0)
                return PropositionUpdateResult.Referenced([.. referrers.Select(node => node.Name)]);
        }
        else
        {
            // Reverting changes what referrers resolve, so it takes the same transactional check
            // as any other edit — the compiled spec may not satisfy every dependent.
            var prospective = new PropositionOverlay(Scope.Overlay);
            prospective.Remove(name);

            var broken = Scope.PrepareClosure(name, prospective, commits);
            if (broken.Count > 0)
                return PropositionUpdateResult.BreaksDependents(broken);
        }

        // The store is the only step in this method that can fail, so it runs first — as Publish
        // does — keeping "all of it, or none" true even though there is no explicit rollback for
        // the in-memory mutations, including dependent commits, that follow.
        _store.Delete(name);
        Scope.CommitClosure(commits);

        _authored.Remove(name);
        Scope.Overlay.Remove(name);
        Scope.Graph.Remove(current.Node);
        // Defensive rather than load-bearing: a proposition is only ever enrolled by Publish,
        // which is also what put the graph edges above, so the two always come and go together.
        Scope.Withdraw(current.Node);

        return PropositionUpdateResult.Removed();
    }

    /// <summary>
    /// One name's authored state, read without taking the scope lock, for a caller already holding
    /// it. A name with no authored document reports <c>Exists: false</c> and version 0 even when a
    /// compiled spec resolves beneath it — authoring over a compiled spec is a creation.
    /// </summary>
    internal (bool Exists, int Version, string? ModelTypeId, string? Description) AuthoredStateCore(string name) =>
        _authored.TryGetValue(name, out var authored)
            ? (true, authored.Version, authored.ModelTypeId, authored.Description)
            : (false, 0, null, null);

    /// <summary>
    /// Parses, cycle-checks and binds a proposed document against <paramref name="source"/> without
    /// publishing anything — the proposition half of a governed publish's validate-everything-first
    /// phase. Passing a prospective source lets one envelope member resolve against another that is
    /// not live yet. Assumes the scope lock is held.
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
    /// Repairing a quarantined proposition via <see cref="Update"/> does not retroactively
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
            var candidates = ReadCandidates();
            _loaded = true;

            // A hand-edited store can contain a reference cycle that Create/Update would have
            // rejected outright — nothing here goes through DependencyGraph.FindCycle. Every member
            // of a detected cycle is quarantined with the real reason instead of being bound.
            var cycles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var ordered = OrderByDependency(candidates, cycles);

            foreach (var (name, cycle) in cycles)
            {
                candidates[name].Errors.Add(new RuleError("$", RuleErrorCode.CycleDetected,
                    $"the stored proposition '{name}' cannot be bound: {string.Join(" → ", cycle)} " +
                    "forms a reference cycle"));
            }

            foreach (var name in ordered)
                LoadOne(candidates[name]);
        });

    /// <summary>
    /// Reads every stored row into a candidate keyed by name, parsing each document up front purely
    /// to order the binding. Name and parse failures are carried forward on the candidate rather
    /// than thrown, so the document is still listed, quarantined, rather than silently dropped.
    /// </summary>
    private Dictionary<string, LoadCandidate> ReadCandidates()
    {
        var candidates = new Dictionary<string, LoadCandidate>(StringComparer.Ordinal);

        // A store is a dumb sink, so a hand-edited or null-serialized one can hand back a null list
        // as readily as a null row — `Deserialize<List<StoredProposition>>("[null]")` yields exactly
        // that. Neither may be fatal: quarantine exists so a bad row costs its own row.
        foreach (var proposition in _store.Load() ?? [])
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

    /// <summary>Binds one stored proposition, publishing it or quarantining it.</summary>
    private void LoadOne(LoadCandidate candidate)
    {
        var stored = candidate.Stored;
        var authored = new Authored(
            this, stored.Name, stored.ModelType, stored.DocumentJson, stored.Version, stored.Description)
        {
            References = candidate.References
        };

        _authored[stored.Name] = authored;

        if (candidate.Errors.Count > 0)
        {
            // A name failure, a parse failure or a cycle already rules out binding — attempting it
            // anyway could only succeed by resolving through a name that is itself unresolvable or
            // by binding on top of an unresolved cyclic reference, neither of which is a real bind.
            authored.Quarantine = candidate.Errors;
            return;
        }

        var errors = new List<RuleError>();
        var commit = authored.PrepareRebind(Scope.Source, errors);

        if (commit is null)
        {
            // Quarantined: no overlay entry and no graph edges, so nothing resolves *to* it and
            // nothing is rebound *because* of it. Any compiled spec under the name still resolves.
            authored.Quarantine = errors;
            return;
        }

        // The same three steps Publish takes to go live, minus the store write — this row came
        // *from* the store, so saving it back would be a no-op at best.
        commit.Commit();
        Scope.Overlay.Set(authored.Bound!);
        Scope.Graph.Set(authored.Node, authored.References);
        Scope.Enrol(authored);
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
    private PropositionModelBinding? ResolveModel(string? modelTypeId, List<RuleError> errors)
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
    private static bool BindsAsync(ISpecSource source, IReadOnlyList<string> references) =>
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

        if (Scope.Graph.FindCycle(name, references) is { } cycle)
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
    /// Publishes an authored proposition and rebinds everything that references its name — all of
    /// it, or none. Shared by <see cref="Create"/> and <see cref="Update"/>, which differ only in
    /// how they arrive here: whether a name may be published at all, and what the outcome is called.
    /// Once a definition is going live under a name, what that does to the name's dependents is the
    /// same question either way, and answering it twice is how the two would drift.
    /// </summary>
    /// <returns>
    /// The dependents that would stop binding, in which case nothing was published. Empty when the
    /// whole closure rebound and <paramref name="authored"/> is live.
    /// </returns>
    private IReadOnlyList<BrokenDependent> PublishWithCascade(Authored authored)
    {
        // Bind the closure against a prospective overlay carrying the new definition, so a dependent
        // is checked against what it *would* resolve rather than what it resolves today.
        var prospective = new PropositionOverlay(Scope.Overlay);
        prospective.Set(authored.Bound!);

        var commits = new List<IRebindCommit>();
        var broken = Scope.PrepareClosure(authored.Name, prospective, commits);
        if (broken.Count > 0)
            return broken;

        Publish(authored);
        Scope.CommitClosure(commits);
        return [];
    }

    /// <summary>
    /// Publishes an authored proposition: store first, then overlay, graph and participant. The
    /// store runs first and is the only step that can fail — none of the in-memory mutations can —
    /// so a store failure leaves nothing live behind it, keeping "all of it, or none" true even
    /// though there is no explicit rollback.
    /// </summary>
    private void Publish(Authored authored)
    {
        _store.Save(new StoredProposition(
            authored.Name, authored.ModelTypeId, authored.DocumentJson, authored.Version, authored.Description));
        _authored[authored.Name] = authored;
        Scope.Overlay.Set(authored.Bound!);
        Scope.Graph.Set(authored.Node, authored.References);
        Scope.Enrol(authored);
    }

    private PropositionEntry ToEntry(Authored authored)
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
    /// One authored proposition's live state, and its participation in the rebind transaction.
    /// </summary>
    private sealed class Authored(
        PropositionSet owner, string name, string modelTypeId, string documentJson, int version, string? description)
        : IRebindable
    {
        public NodeId Node { get; } = NodeId.Proposition(name);
        public string Name { get; } = name;
        public string ModelTypeId { get; } = modelTypeId;
        public string DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public string? Description { get; } = description;

        /// <summary>The current binding, or null while quarantined.</summary>
        public SpecRegistryEntry? Bound { get; set; }

        /// <summary>Why this proposition is excluded from the effective set, or empty.</summary>
        public IReadOnlyList<RuleError> Quarantine { get; set; } = [];

        public IReadOnlyList<string> References { get; set; } = [];

        public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
        {
            var model = owner.ResolveModel(ModelTypeId, errors);
            if (model is null)
                return null;

            var document = new RuleDocumentParser(owner._options).Parse(DocumentJson, errors);
            if (document is null || errors.Count > 0)
                return null;

            var isAsync = BindsAsync(prospective, References);
            var entry = model.Bind(prospective, Name, Description, document, isAsync, errors);
            return entry is null ? null : new RebindCommit(this, entry);
        }

        private sealed class RebindCommit(Authored authored, SpecRegistryEntry entry) : IRebindCommit
        {
            public SpecRegistryEntry? OverlayEntry => entry;

            public void Commit()
            {
                authored.Bound = entry;
                authored.Quarantine = [];
                // The version is deliberately untouched: this proposition's document did not change,
                // so bumping it would spuriously conflict with an editor's open draft.
            }
        }
    }
}
