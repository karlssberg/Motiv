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

    /// <summary>Creates a rule set whose documents bind against the given registry.</summary>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    public RuleSet(SpecRegistry registry, RuleSerializerOptions? options = null)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        _serializer = new RuleSerializer(registry, options);
    }

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
        return this;
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
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public RuleUpdateResult Update(string name, string documentJson, int expectedVersion)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));

        return Find(name) is { } rule
            ? rule.TryUpdate(_serializer, documentJson, expectedVersion)
            : RuleUpdateResult.NotFound();
    }

    /// <summary>Reverts a rule to its default. The version moves forward, never back.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid default document, or not found.</returns>
    public RuleUpdateResult Revert(string name, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.TryRevert(_serializer, expectedVersion)
            : RuleUpdateResult.NotFound();
}
