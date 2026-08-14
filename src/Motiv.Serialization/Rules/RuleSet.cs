namespace Motiv.Serialization;

/// <summary>
/// The set of live rules an application executes. Adding a rule binds its default immediately
/// (fail-fast at startup); <see cref="Update"/> and <see cref="Revert"/> validate and bind
/// first, then publish atomically — writers get optimistic version conflicts, evaluators
/// always see a coherent snapshot.
/// </summary>
/// <remarks>
/// Like <see cref="SpecRegistry"/>, registration (<see cref="Add"/>) is intended to finish at
/// startup before concurrent use; <see cref="Update"/>/<see cref="Revert"/>/lookups are safe
/// concurrently thereafter.
/// </remarks>
public sealed class RuleSet
{
    private readonly Dictionary<string, RuleBase> _rules = new(StringComparer.Ordinal);
    private readonly RuleSerializer _serializer;
    private readonly RuleSerializerOptions _options;

    /// <summary>Creates a rule set whose documents bind against the given registry.</summary>
    /// <remarks>
    /// For a host that also authors propositions, build the rule set from the
    /// <see cref="PropositionSet"/> instead — see
    /// <see cref="RuleSet(PropositionSet, RuleSerializerOptions)"/>. This overload opens a binding
    /// scope of its own, which a proposition set built from the same registry could never see into.
    /// </remarks>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// A <see cref="PropositionSet"/> was already built from <paramref name="registry"/>, so this set
    /// could only ever be invisible to it.
    /// </exception>
    public RuleSet(SpecRegistry registry, RuleSerializerOptions? options = null)
        : this(BindingScope.For(registry, ScopeClaim.Rules), options)
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
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="propositions"/> is null.</exception>
    public RuleSet(PropositionSet propositions, RuleSerializerOptions? options = null)
        : this((propositions ?? throw new ArgumentNullException(nameof(propositions))).Scope, options)
    {
    }

    /// <summary>
    /// Creates a rule set sharing a <see cref="BindingScope"/> with a <see cref="PropositionSet"/>, so
    /// a proposition edit and a rule update cannot interleave and a rule can be rebound by a
    /// proposition's republication.
    /// </summary>
    internal RuleSet(BindingScope scope, RuleSerializerOptions? options = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
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
            version, rule.Description, documentJson);
    }

    /// <summary>
    /// Replaces a rule's implementation with a document: validate → bind → atomic publish.
    /// The live rule is untouched unless the document binds and the expected version holds.
    /// </summary>
    /// <remarks>
    /// Runs under the <see cref="BindingScope"/> write lock, so concurrent writes to any rule in
    /// the set serialize rather than interleave — a rule can now be rebound out from under a
    /// caller by someone else's proposition edit, and <c>RebindCommit</c> publishes by writing the
    /// rule's state directly too, so the two must never run concurrently. What refuses a stale
    /// write is the expected-version check in <see cref="PrepareUpdateCore"/>, taken before the
    /// lock is ever released — a caller holding an old version is still told the current one,
    /// only now that refusal is decided by two writes queuing instead of racing.
    /// </remarks>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public RuleUpdateResult Update(string name, string documentJson, int expectedVersion)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));

        return Scope.Locked(() =>
        {
            var prepared = PrepareUpdateCore(name, documentJson, expectedVersion);
            return prepared.Publication is { } publication
                ? CommitCore(name, publication)
                : prepared.ToFailureResult();
        });
    }

    /// <summary>Reverts a rule to its default. The version moves forward, never back.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid default document, or not found.</returns>
    public RuleUpdateResult Revert(string name, int expectedVersion) =>
        Scope.Locked(() =>
        {
            var prepared = PrepareRevertCore(name, expectedVersion);
            return prepared.Publication is { } publication
                ? CommitCore(name, publication)
                : prepared.ToFailureResult();
        });

    /// <summary>
    /// Prepares an update without publishing it, for a caller already holding the scope lock. The
    /// caller persists the prepared version, then commits — see <see cref="CommitCore"/>. The split
    /// exists so that everything fallible runs before anything mutates.
    /// </summary>
    /// <remarks>
    /// Two splits meet here. The <c>Core</c> suffix is the lock split: the lock is a plain monitor
    /// and therefore reentrant, so calling <see cref="Update"/> from inside it would not in fact
    /// deadlock. That split is about the *unit of atomicity* — a caller that means "these edits
    /// publish together" must not be able to express it as a series of calls each of which releases
    /// the lock in between. <c>Prepare</c>/<c>Commit</c> is the publish split, and is about where a
    /// step that can still fail — the store write — is allowed to sit.
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
    /// Commits a prepared publication and re-tracks the rule's graph edges. Has no failure outcome —
    /// everything a caller can get wrong was already decided by the prepare. Assumes the scope lock
    /// is held.
    /// </summary>
    internal RuleUpdateResult CommitCore(string name, IRulePublication publication)
    {
        // Resolved before the commit so that the unreachable arm fails with nothing yet moved.
        // A publication only exists because a Prepare found the rule, the scope lock has been held
        // throughout, and rules are never unregistered — so this cannot miss. It throws rather than
        // skipping the tracking below, which would leave the rule published with stale graph edges.
        var rule = Find(name)
            ?? throw new InvalidOperationException(
                $"Rule '{name}' is no longer registered, so its prepared publication cannot be committed.");

        publication.Commit();

        // Track reads the rule's *current* document, so it must run after the commit, not before.
        Track(rule);

        return RuleUpdateResult.Updated(publication.Version);
    }

    /// <summary>
    /// Binds a proposed document against <paramref name="source"/> without publishing anything, so a
    /// governed publish can discover that a rule half of an envelope would not bind while nothing
    /// has moved yet. Passing a prospective source is the point: an envelope's rule edit may
    /// reference a proposition the same envelope creates, which the live source cannot resolve.
    /// Assumes the scope lock is held.
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
    internal IReadOnlyList<RuleError> ValidateDefaultCore(string name, ISpecSource source)
    {
        if (Find(name) is not { Default.DocumentJson: { } documentJson } rule)
            return [];

        var errors = new List<RuleError>();
        rule.ValidateDocument(new RuleSerializer(source, _options), documentJson, errors);
        return errors;
    }

    /// <summary>
    /// Records the rule's current outgoing references and its participation in rebinds. A rule on a
    /// compiled default resolves no names, so it leaves the graph entirely.
    /// </summary>
    private void Track(RuleBase rule)
    {
        var node = NodeId.Rule(rule.Name);
        var references = ReferencesOf(rule.DocumentJson);

        if (references.Count == 0)
        {
            Scope.Graph.Remove(node);
            // Defensive rather than load-bearing: a rule is only ever enrolled by the branch below,
            // which is also what put the graph edges there, so the two always come and go together.
            Scope.Withdraw(node);
            return;
        }

        Scope.Graph.Set(node, references);
        Scope.Enrol(new RuleParticipant(rule, _options));
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
