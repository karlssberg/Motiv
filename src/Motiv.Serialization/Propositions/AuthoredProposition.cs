namespace Motiv.Serialization;

/// <summary>
/// One authored proposition's state within a generation, and its participation in the rebind
/// transaction. Immutable: a rebind produces a replacement rather than editing this one, because the
/// generation holding it is published to lock-free readers and must not change underneath them.
/// </summary>
/// <remarks>
/// "Immutable" describes this instance's own state, not the graph it can reach: it keeps a back-pointer
/// to the mutable <see cref="PropositionSet"/> that owns it, purely so <see cref="Rebind"/> can reach
/// <c>ResolveModel</c> and <c>Options</c>. <see cref="RebindCommit.Commit"/> uses that same back-pointer
/// — via <see cref="Owner"/> — to write the rebound replacement into the owning set's own live
/// dictionary, which is not yet part of the generation. That coupling is exactly what made it easy to
/// lose that write during the first cut of this class; see <see cref="Owner"/>'s remarks.
/// </remarks>
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
    /// <summary>
    /// The set this proposition belongs to. Exists so a rebind's <see cref="RebindCommit"/> can refresh
    /// this set's own authored dictionary at commit time — see
    /// <see cref="PropositionSet.SetAuthoredState"/>. A true value type would not need this; it stays
    /// only because <c>PropositionSet._authored</c> has not yet moved into the generation.
    /// </summary>
    internal PropositionSet Owner => owner;

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
        /// The one write <see cref="ApplyTo"/> cannot make: <see cref="PropositionSet"/> still keeps
        /// its own live dictionary of authored propositions — <c>_authored</c> — and that dictionary,
        /// not the generation, is what the public listing (<c>Find</c>, <c>Propositions</c>,
        /// <c>DocumentJsonOf</c>) reads. <see cref="ApplyTo"/> only reaches the generation's own
        /// authored map, via the builder, so without this a cascaded dependent's live entry would go
        /// stale — its <c>Bound</c> would keep pointing at what it resolved to *before* this rebind,
        /// which is exactly the kind of drift a generation swap exists to make impossible. This has to
        /// happen here rather than in <see cref="ApplyTo"/> because <see cref="BindingScope.PrepareClosure"/>
        /// also calls <see cref="ApplyTo"/>, against a prospective world that may still be discarded —
        /// a live write from there would publish a binding the caller went on to reject.
        /// <see cref="Commit"/> is reached only from <see cref="ScopeGenerationBuilder.Apply"/>, i.e.
        /// at commit time, which is the right moment for it. Retires once
        /// <see cref="ScopeGeneration.Authored"/> becomes the read path and <c>PropositionSet._authored</c>
        /// is deleted.
        /// </summary>
        public void Commit() => rebound.Owner.SetAuthoredState(rebound);
    }
}
