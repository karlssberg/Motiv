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
    /// rule's state directly rather than through the compare-and-swap here, so the two must never
    /// run concurrently. The compare-and-swap itself is unchanged and still what refuses a stale
    /// write: a caller holding an old version is still told the current one, only now that
    /// refusal is decided by two writes queuing instead of racing.
    /// </remarks>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public RuleUpdateResult Update(string name, string documentJson, int expectedVersion)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));

        return Scope.Locked(() => UpdateCore(name, documentJson, expectedVersion));
    }

    /// <summary>Reverts a rule to its default. The version moves forward, never back.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid default document, or not found.</returns>
    public RuleUpdateResult Revert(string name, int expectedVersion) =>
        Scope.Locked(() => RevertCore(name, expectedVersion));

    /// <summary>
    /// <see cref="Update"/> without taking the scope lock, for a caller already holding it — a
    /// governed publish takes the lock once and drives several of these, so that the whole envelope
    /// is one atomic step rather than a sequence of individually-atomic ones.
    /// </summary>
    /// <remarks>
    /// The lock is a plain monitor and therefore reentrant, so calling the public method from inside
    /// it would not in fact deadlock. The split is about the *unit of atomicity*, not about
    /// deadlock avoidance: a caller that means "these edits publish together" must not be able to
    /// express it as a series of calls each of which releases the lock in between.
    /// </remarks>
    internal RuleUpdateResult UpdateCore(string name, string documentJson, int expectedVersion) =>
        MutateCore(name, rule => rule.TryUpdate(_serializer, documentJson, expectedVersion));

    /// <summary><see cref="Revert"/> without taking the scope lock. See <see cref="UpdateCore"/>.</summary>
    internal RuleUpdateResult RevertCore(string name, int expectedVersion) =>
        MutateCore(name, rule => rule.TryRevert(_serializer, expectedVersion));

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
    /// Looks up a rule and applies a publish operation to it — the shared shape behind
    /// <see cref="UpdateCore"/> and <see cref="RevertCore"/>: find-or-not-found, publish, then
    /// re-track the rule's graph edges whenever the publish actually took. Assumes the scope lock
    /// is held.
    /// </summary>
    private RuleUpdateResult MutateCore(string name, Func<RuleBase, RuleUpdateResult> publish)
    {
        if (Find(name) is not { } rule)
            return RuleUpdateResult.NotFound();

        var result = publish(rule);
        if (result.Outcome == RuleUpdateOutcome.Updated)
            Track(rule);
        return result;
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
