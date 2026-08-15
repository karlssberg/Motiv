namespace Motiv.Serialization;

/// <summary>
/// The non-generic identity of a rule: a named, versioned, hot-swappable decision handle.
/// Concrete rules derive from <see cref="Rule{TModel,TMetadata}"/>,
/// <see cref="PolicyRule{TModel,TMetadata}"/>, <see cref="AsyncRule{TModel,TMetadata}"/>, or
/// <see cref="AsyncPolicyRule{TModel,TMetadata}"/>.
/// </summary>
public abstract class RuleBase
{
    private protected RuleBase(string name, RuleDefault @default, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A rule name must not be empty or whitespace.", nameof(name));

        Name = name;
        Default = @default;
        Description = description;
    }

    /// <summary>The stable name the rule is addressed by (endpoints, RuleSet lookups).</summary>
    public string Name { get; }

    /// <summary>An optional human-readable description surfaced by the rules endpoints.</summary>
    public string? Description { get; }

    /// <summary>The model type the rule evaluates against.</summary>
    public abstract Type ModelType { get; }

    /// <summary>The metadata type the rule yields.</summary>
    public abstract Type MetadataType { get; }

    /// <summary>Whether the rule evaluates asynchronously.</summary>
    public abstract bool IsAsync { get; }

    /// <summary>Whether the rule is policy-flavoured (yields a single value).</summary>
    public abstract bool IsPolicy { get; }

    /// <summary>The current version, starting at 1 and incremented by every successful update or revert.</summary>
    /// <remarks>
    /// The <em>live</em> version, even inside an open pin — this is the number a writer passes back as
    /// <c>expectedVersion</c>, so it must name the world a publish would commit into. The catalog
    /// listing's version, which is a read for display, comes from <see cref="VersionedDocument"/> and
    /// is pinned instead. See <see cref="BindingScope.Active"/>.
    /// </remarks>
    public abstract int Version { get; }

    /// <summary>The current document JSON, or null while the rule is on a compiled default.</summary>
    /// <remarks>The live document, for the same reason <see cref="Version"/> is the live version.</remarks>
    public abstract string? DocumentJson { get; }

    /// <summary>
    /// The document this rule will carry once <paramref name="builder"/> is published. Read from the
    /// builder rather than the live world because <see cref="RuleSet"/> re-tracks graph edges from the
    /// document a publish is *about to* make live.
    /// </summary>
    internal abstract string? DocumentJsonIn(ScopeGenerationBuilder builder);

    /// <summary>
    /// The document this rule carried in <paramref name="generation"/>, or null when it was on a
    /// compiled default or had no state there at all.
    /// </summary>
    /// <remarks>
    /// The named-world counterpart of <see cref="DocumentJson"/>, which reads whatever is live at the
    /// moment it is called. A caller that has already snapshotted a world and is deciding something
    /// about it must ask that world, not the current one: mixing the two means judging a rule's state
    /// against a document from a different world, and a decision taken on that mismatch has escaped
    /// whatever compare-and-set the snapshot was taken for. Unlike <see cref="Version"/> and
    /// <see cref="DocumentJson"/> this never throws for an unbound slot — a caller reconstructing a
    /// world needs "there was nothing there" as an answer, not an exception.
    /// </remarks>
    internal abstract string? DocumentJsonIn(ScopeGeneration generation);

    internal RuleDefault Default { get; }

    private BindingScope? _scope;

    /// <summary>Where this rule's state lives in every generation. Assigned once, by <see cref="RuleSet.Add"/>.</summary>
    internal int Slot { get; private set; } = -1;

    /// <summary>
    /// The scope holding this rule's worlds. Throws the same message the old unbound-state check gave,
    /// because it is the message a developer sees when they evaluate a rule they never registered.
    /// </summary>
    internal BindingScope Scope =>
        _scope ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    /// <summary>
    /// Refuses a rule that already belongs to a scope. Called by <see cref="RuleSet.Add"/> before it
    /// binds the default, so re-adding a rule reports that rather than whatever the second registry
    /// makes of its document, and again by <see cref="Occupy"/>, which is the actual claim.
    /// </summary>
    internal void EnsureUnbound()
    {
        if (_scope is not null)
            throw new InvalidOperationException($"Rule '{Name}' has already been added to a RuleSet.");
    }

    /// <summary>Claims a permanent slot in the scope's generations. Called exactly once, by <see cref="RuleSet.Add"/>.</summary>
    internal void Occupy(BindingScope scope, int slot)
    {
        EnsureUnbound();

        _scope = scope;
        Slot = slot;
    }

    /// <summary>
    /// Why <see cref="RuleSet.Load"/> could not apply this rule's stored document, or empty. Read out
    /// of the generation rather than held here, so it moves with the binding it describes: a
    /// quarantine that lagged its own binding would report a rule broken after the publish that
    /// repaired it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads <see cref="BindingScope.Active"/>, not <see cref="BindingScope.Current"/>. Its only caller
    /// is the catalog listing, and it must come from the same world as the
    /// <see cref="VersionedDocument"/> it is rendered beside — see there.
    /// </para>
    /// <para>
    /// Reached through <c>_scope</c> rather than the throwing <see cref="Scope"/> property, because an
    /// unregistered rule has no quarantine rather than an error, which is what the field this replaced
    /// reported.
    /// </para>
    /// </remarks>
    internal IReadOnlyList<RuleError> Quarantine
    {
        get
        {
            // Slot is read rather than assumed non-negative: Occupy writes the scope and the slot as
            // two plain writes, so a racing reader can see the first without the second.
            if (_scope is null || Slot < 0)
                return [];

            var slots = _scope.Active.RuleSlots;
            return Slot < slots.Length && slots[Slot] is { } slot ? slot.Quarantine : [];
        }
    }

    /// <summary>Binds the default and produces the state version 1 will publish.</summary>
    internal abstract object BindDefaultState(RuleSerializer serializer);

    /// <summary>
    /// The same state at a different version number — used only by <see cref="RuleSet.Load"/>, to
    /// restore the number the store holds after a stored document has been bound through the ordinary
    /// publish path. Renumbering anywhere else would break the optimistic-concurrency contract.
    /// </summary>
    internal abstract object WithVersion(object state, int version);

    /// <summary>
    /// Binds <paramref name="documentJson"/> — or the compiled default, when it is null — as this
    /// rule's state at <paramref name="version"/>. What <see cref="RuleSet.RefreshAsync"/> rebuilds a
    /// slot with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Deliberately not <see cref="PrepareUpdate"/>.</strong> That is a <em>publish</em>: it
    /// checks a caller's expected version against the live world, mints the next version, and hands
    /// back a publication to commit once the store has accepted it. A refresh does none of those
    /// things. The store's version is already authoritative — a rebuild that renumbered would rewrite
    /// history — and there is no caller holding a number that could be stale, because the numbers
    /// being reconciled both came from the store.
    /// </para>
    /// <para>
    /// Routing a rebuild through the publish path is not merely redundant, it is wrong in a way that
    /// only shows up in production. The rebuild would have to invent an expected version to satisfy
    /// the check, and the only defensible invention is the one the slot was just re-bound to. But
    /// <c>PrepareUpdate</c> reads the version from the <em>live</em> world, not from the successor
    /// being built — so on any replica past version 1 the check fails, the head is read as a document
    /// that no longer binds, and the refresh aborts. Every subsequent refresh aborts identically: a
    /// replica that converged once could never converge again. Binding without a version check is not
    /// a relaxation of optimistic concurrency; it is the recognition that a rebuild is not a write.
    /// </para>
    /// </remarks>
    /// <param name="serializer">Binds against the successor's source, so it sees this refresh's authored layer.</param>
    /// <param name="documentJson">The stored document, or null for "on the compiled default at this version".</param>
    /// <param name="version">The version the store holds, which the rebuilt slot must report.</param>
    /// <param name="errors">Filled with why the document would not bind, when it would not.</param>
    /// <returns>The state to write into the slot, or null when it did not bind.</returns>
    internal abstract object? BindStoredState(
        RuleSerializer serializer, string? documentJson, int version, List<RuleError> errors);

    /// <summary>
    /// Validates and binds the document against <paramref name="expectedVersion"/>, returning a
    /// publication that is not yet live. Binding is the fallible half; committing is not — the split
    /// is what lets the store write sit between them.
    /// </summary>
    internal abstract RulePrepareResult PrepareUpdate(
        RuleSerializer serializer, string documentJson, int expectedVersion);

    /// <summary>
    /// Binds the default against <paramref name="expectedVersion"/>, returning a publication that
    /// moves the version <em>forward</em> — a revert is a new version, never a return to an old one.
    /// </summary>
    internal abstract RulePrepareResult PrepareRevert(RuleSerializer serializer, int expectedVersion);

    /// <summary>
    /// Binds the rule's current document against a prospective source without publishing, so a
    /// proposition edit can discover that this rule would stop binding while nothing has moved yet.
    /// Returns a no-op commit for a rule on its compiled default, which references nothing.
    /// </summary>
    /// <param name="serializer">Binds the document against the prospective source.</param>
    /// <param name="world">
    /// The world holding the document and version being rebound. Passed in rather than read from
    /// <c>Scope.Current</c> here, so this rebind and the graph walk that reached it come from one
    /// world — see <see cref="IRebindable.PrepareRebind"/>.
    /// </param>
    /// <param name="errors">Filled with why the rule would no longer bind.</param>
    internal abstract IRebindCommit? PrepareRebind(
        RuleSerializer serializer, ScopeGeneration world, List<RuleError> errors);

    /// <summary>
    /// Binds a *proposed* document against the serializer's source, publishing nothing and leaving
    /// the version untouched — the dry run behind a governed publish's validate-everything-first
    /// phase. Distinct from <see cref="PrepareRebind"/>, which re-binds the document the rule
    /// already carries; here the document is one the rule has never seen.
    /// </summary>
    internal abstract void ValidateDocument(RuleSerializer serializer, string documentJson, List<RuleError> errors);

    /// <summary>Reads the version and document from one snapshot, so the pair is always coherent.</summary>
    /// <remarks>
    /// Serves <see cref="RuleSet.Rules"/>/<see cref="RuleSet.FindEntry"/> and nothing else, so it reads
    /// the <em>pinned</em> world: a catalog listing binds nothing and publishes nothing, and a pinned
    /// request stamps the generation it pinned onto its response, so a listing read from the live world
    /// would describe a world its own header disclaims — and <c>GET /rules</c> would disagree with
    /// <c>GET /propositions</c> served in the same request. See <see cref="BindingScope.Active"/>.
    /// </remarks>
    internal abstract (int Version, string? DocumentJson) VersionedDocument();
}
