namespace Motiv.Serialization;

/// <summary>
/// The authored propositions an application resolves alongside its compiled ones. Mirrors
/// <see cref="RuleSet"/>: validate, bind, then publish atomically, with optimistic version checks on
/// writes. Unlike a rule, a proposition is *referenceable*, so publishing one also rebinds everything
/// that references it — all of it, or none.
/// </summary>
public sealed class PropositionSet
{
    private readonly BindingScope _scope;
    private readonly IPropositionStore _store;
    private readonly RuleSerializerOptions _options;
    private readonly Dictionary<string, PropositionModelBinding> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Authored> _authored = new(StringComparer.Ordinal);

    internal PropositionSet(BindingScope scope, IPropositionStore store, RuleSerializerOptions? options = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new RuleSerializerOptions();
    }

    /// <summary>
    /// Registers a model type authored propositions may be written against, capturing
    /// <typeparamref name="TModel"/> behind a closure so no binding step needs reflection.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type.</typeparam>
    /// <param name="modelTypeId">The stable id clients pass as <c>modelType</c>.</param>
    /// <returns>This set, to allow chained registration.</returns>
    public PropositionSet AddModel<TModel>(string modelTypeId) =>
        _scope.Locked(() =>
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
        _scope.Locked(() =>
        {
            var entries = new Dictionary<string, PropositionEntry>(StringComparer.Ordinal);

            foreach (var compiled in _scope.Registry.Entries)
                entries[compiled.Name] = ToEntry(compiled);

            foreach (var authored in _authored.Values)
                entries[authored.Name] = ToEntry(authored);

            return (IReadOnlyCollection<PropositionEntry>)[.. entries.Values];
        });

    /// <summary>One proposition's listing, or null when the name is unknown.</summary>
    public PropositionEntry? Find(string name) =>
        _scope.Locked<PropositionEntry?>(() =>
        {
            if (_authored.TryGetValue(name, out var authored))
                return ToEntry(authored);

            return _scope.Registry.Find(name) is { } compiled ? ToEntry(compiled) : null;
        });

    /// <summary>The authored document behind a name, or null when the name has no authored document.</summary>
    public string? DocumentJsonOf(string name) =>
        _scope.Locked(() => _authored.TryGetValue(name, out var authored) ? authored.DocumentJson : null);

    /// <summary>The nodes that reference the given proposition, transitively, in rebind order.</summary>
    public IReadOnlyList<PropositionDependent> Dependents(string name) =>
        _scope.Locked(() => (IReadOnlyList<PropositionDependent>)
            [.. _scope.Graph.DependentClosure(name)
                .Select(node => new PropositionDependent(
                    node.Name, node.Kind == NodeKind.Rule ? "rule" : "proposition"))]);

    /// <summary>
    /// Authors a new proposition. A name already carrying an authored document is a conflict; a name
    /// carrying only a compiled spec is accepted and creates an override.
    /// </summary>
    /// <param name="name">The dot-separated name.</param>
    /// <param name="modelTypeId">A registered model-type id.</param>
    /// <param name="documentJson">The rule document defining the proposition.</param>
    /// <param name="description">An optional description.</param>
    /// <returns>The outcome. Nothing is published or persisted unless it is <c>Created</c>.</returns>
    public PropositionUpdateResult Create(
        string name, string modelTypeId, string documentJson, string? description) =>
        _scope.Locked(() =>
        {
            if (_authored.ContainsKey(name))
                return PropositionUpdateResult.NameTaken();

            if (ValidateName(name) is { } nameError)
                return PropositionUpdateResult.Invalid([nameError]);

            var prepared = Prepare(name, modelTypeId, documentJson, description);
            if (prepared.Entry is not { } entry)
                return PropositionUpdateResult.Invalid(prepared.Errors);

            // A brand-new name has no referrers, so the closure is empty and there is nothing to
            // cascade to — but the document may still reference the name being created.
            Publish(new Authored(this, name, modelTypeId, documentJson, version: 1, description)
            {
                Bound = entry,
                References = prepared.References
            });

            return PropositionUpdateResult.Created(1);
        });

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
    /// The binder registered for a model-type id, or null once the mismatch has been recorded.
    /// </summary>
    private PropositionModelBinding? ResolveModel(string modelTypeId, List<RuleError> errors)
    {
        if (_models.TryGetValue(modelTypeId, out var model))
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
    private Prepared Prepare(string name, string modelTypeId, string documentJson, string? description)
    {
        var errors = new List<RuleError>();

        var model = ResolveModel(modelTypeId, errors);
        if (model is null)
            return new Prepared(null, [], errors);

        var document = new RuleDocumentParser(_options).Parse(documentJson, errors);
        if (document is null || errors.Count > 0)
            return new Prepared(null, [], errors);

        var references = DocumentReferences.From(document);

        if (_scope.Graph.FindCycle(name, references) is { } cycle)
        {
            errors.Add(new RuleError("$", RuleErrorCode.CycleDetected,
                $"publishing '{name}' would create a reference cycle: {string.Join(" → ", cycle)}"));
            return new Prepared(null, references, errors);
        }

        var isAsync = BindsAsync(_scope.Source, references);
        var entry = model.Bind(_scope.Source, name, description, document, isAsync, errors);
        return new Prepared(entry, references, errors);
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
        _scope.Overlay.Set(authored.Bound!);
        _scope.Graph.Set(authored.Node, authored.References);
        _scope.Enrol(authored);
    }

    private PropositionEntry ToEntry(Authored authored)
    {
        var compiled = _scope.Registry.Find(authored.Name);
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
