namespace Motiv.Serialization;

/// <summary>
/// One authored proposition's state within a generation, and its participation in the rebind
/// transaction. Immutable: a rebind produces a replacement rather than editing this one, because the
/// generation holding it is published to lock-free readers and must not change underneath them.
/// </summary>
internal sealed class AuthoredProposition(
    PropositionSet owner,
    string name,
    string modelTypeId,
    string documentJson,
    int version,
    string? description,
    SpecRegistryEntry? bound,
    IReadOnlyList<RuleError> quarantine,
    IReadOnlyList<string> references)
    : IRebindable
{
    public NodeId Node { get; } = NodeId.Proposition(name);
    public string Name { get; } = name;
    public string ModelTypeId { get; } = modelTypeId;
    public string DocumentJson { get; } = documentJson;
    public int Version { get; } = version;
    public string? Description { get; } = description;

    /// <summary>The current binding, or null while quarantined.</summary>
    public SpecRegistryEntry? Bound { get; } = bound;

    /// <summary>Why this proposition is excluded from the effective set, or empty.</summary>
    public IReadOnlyList<RuleError> Quarantine { get; } = quarantine;

    /// <summary>The names this proposition's document resolves.</summary>
    public IReadOnlyList<string> References { get; } = references;

    /// <summary>
    /// The same proposition, rebound. The version is deliberately carried across: the document did
    /// not change, only what it resolves to, so bumping it would spuriously conflict with an editor's
    /// open draft. Quarantine is dropped — binding again is what resolves one.
    /// </summary>
    public AuthoredProposition WithBinding(SpecRegistryEntry rebound) =>
        new(owner, Name, ModelTypeId, DocumentJson, Version, Description, rebound, [], References);

    /// <summary>The same proposition, excluded from the effective set with the reasons why.</summary>
    public AuthoredProposition WithQuarantine(IReadOnlyList<RuleError> quarantine) =>
        new(owner, Name, ModelTypeId, DocumentJson, Version, Description, Bound, quarantine, References);

    /// <summary>
    /// The replacement this proposition would become when bound against <paramref name="prospective"/>,
    /// or null with the reasons in <paramref name="errors"/> when it would no longer bind. Publishes
    /// nothing: the replacement is an object until a caller writes it into a generation, so building it
    /// during the prepare phase cannot leak a binding that is later rejected.
    /// </summary>
    public AuthoredProposition? Rebind(ISpecSource prospective, List<RuleError> errors)
    {
        var model = owner.ResolveModel(ModelTypeId, errors);
        if (model is null)
            return null;

        var document = new RuleDocumentParser(owner.Options).Parse(DocumentJson, errors);
        if (document is null || errors.Count > 0)
            return null;

        var isAsync = PropositionSet.BindsAsync(prospective, References);
        var entry = model.Bind(prospective, Name, Description, document, isAsync, errors);
        return entry is null ? null : WithBinding(entry);
    }

    public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors) =>
        Rebind(prospective, errors) is { } rebound ? new RebindCommit(rebound) : null;

    private sealed class RebindCommit(AuthoredProposition rebound) : IRebindCommit
    {
        public void ApplyTo(ScopeGenerationBuilder builder)
        {
            builder.SetAuthored(rebound);
            builder.SetOverlayEntry(rebound.Bound!);
        }

        /// <summary>
        /// Retired: <see cref="ApplyTo"/> carries the replacement into the builder itself, so an
        /// authored rebind has nothing left to write here. Kept only because <see cref="IRebindCommit"/>
        /// still declares the member for the rule half — see its remarks.
        /// </summary>
        public void Commit()
        {
        }
    }
}
