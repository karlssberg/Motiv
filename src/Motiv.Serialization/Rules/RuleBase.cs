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
    public abstract int Version { get; }

    /// <summary>The current document JSON, or null while the rule is on a compiled default.</summary>
    public abstract string? DocumentJson { get; }

    /// <summary>
    /// The document this rule will carry once <paramref name="builder"/> is published. Read from the
    /// builder rather than the live world because <see cref="RuleSet"/> re-tracks graph edges from the
    /// document a publish is *about to* make live.
    /// </summary>
    internal abstract string? DocumentJsonIn(ScopeGenerationBuilder builder);

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
    /// Reads <see cref="BindingScope.Current"/> through <c>_scope</c> rather than <see cref="Scope"/>:
    /// an unregistered rule has no quarantine rather than an error, which is what the field this
    /// replaced reported.
    /// </remarks>
    internal IReadOnlyList<RuleError> Quarantine
    {
        get
        {
            // Slot is read rather than assumed non-negative: Occupy writes the scope and the slot as
            // two plain writes, so a racing reader can see the first without the second.
            if (_scope is null || Slot < 0)
                return [];

            var slots = _scope.Current.RuleSlots;
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
    internal abstract IRebindCommit? PrepareRebind(RuleSerializer serializer, List<RuleError> errors);

    /// <summary>
    /// Binds a *proposed* document against the serializer's source, publishing nothing and leaving
    /// the version untouched — the dry run behind a governed publish's validate-everything-first
    /// phase. Distinct from <see cref="PrepareRebind"/>, which re-binds the document the rule
    /// already carries; here the document is one the rule has never seen.
    /// </summary>
    internal abstract void ValidateDocument(RuleSerializer serializer, string documentJson, List<RuleError> errors);

    /// <summary>Reads the version and document from one snapshot, so the pair is always coherent.</summary>
    internal abstract (int Version, string? DocumentJson) VersionedDocument();
}
