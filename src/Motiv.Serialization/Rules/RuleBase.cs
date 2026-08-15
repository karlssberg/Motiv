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
    /// The document a rule tracker should read while a commit is still assembling
    /// <paramref name="builder"/> — the one the builder is about to publish, not necessarily the one
    /// <see cref="DocumentJson"/> currently reports. Today the two always agree: a rule's state is a
    /// field on the rule itself, so <see cref="IRulePublication.ApplyTo"/> has already swapped it by
    /// the time <c>Track</c> calls this. The distinction exists so a future move of that state into
    /// <paramref name="builder"/> only changes the implementation here, not every caller.
    /// </summary>
    internal abstract string? DocumentJsonIn(ScopeGenerationBuilder builder);

    internal RuleDefault Default { get; }

    // Volatile, not a plain auto-property: RuleSet.Rules/FindEntry/ToEntry read this without
    // holding the scope lock, while RuleSet.Apply and CommitCore write it under the lock. Without
    // a memory barrier a lock-free reader could observe a stale value past the write — e.g. a
    // repair clearing quarantine under CommitCore while a concurrent FindEntry still sees it set.
    private IReadOnlyList<RuleError> _quarantine = [];

    /// <summary>
    /// Why <see cref="RuleSet.Load"/> could not apply this rule's stored document, or empty. Non-empty
    /// means the rule is evaluating its compiled default while the store holds something that would
    /// not bind. Held here rather than in a table beside <see cref="RuleSet"/>'s rules — as
    /// <c>PropositionSet</c> holds it on the authored proposition — so that a single reference write
    /// keeps it coherent for the unsynchronised readers of <see cref="RuleSet.Rules"/>.
    /// </summary>
    internal IReadOnlyList<RuleError> Quarantine
    {
        get => Volatile.Read(ref _quarantine);
        set => Volatile.Write(ref _quarantine, value);
    }

    /// <summary>Binds the default and publishes version 1. Called exactly once, by <see cref="RuleSet.Add"/>.</summary>
    internal abstract void Attach(RuleSerializer serializer);

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

    /// <summary>
    /// Overwrites the live version without touching the binding — used only by
    /// <see cref="RuleSet.Load"/>, to restore the number the store holds after a stored document has
    /// been bound through the ordinary publish path. Renumbering anywhere else would break the
    /// optimistic-concurrency contract.
    /// </summary>
    internal abstract void RestoreVersion(int version);
}
