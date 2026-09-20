namespace Motiv.Serialization;

/// <summary>A proposition document at a pinned version, as a reproduction or a snapshot hands it to the binder.</summary>
internal sealed record PinnedDocument(string Name, int Version, string? ModelType, string? DocumentJson, string? Description);

/// <summary>
/// Binds pinned proposition documents, in dependency order, into a transient overlay over a live
/// source, so a pinned version shadows today's head for exactly the pinned names while everything
/// unpinned resolves as production does. A document that will not bind is noted and its dependents
/// are left unbound — never quietly resolved through today's head — as is anything caught in a
/// reference cycle. Shared by the reproducer and the snapshot package, so a generated test binds
/// exactly as the reproduction did.
/// </summary>
internal static class PinnedPropositionBinder
{
    /// <param name="documents">The pinned documents; every name here is a pinned name, parsed or not.</param>
    /// <param name="live">What lies beneath the overlay: the live source, or a bare registry.</param>
    /// <param name="propositions">Supplies the model bindings and the parser options.</param>
    /// <param name="notes">Receives a <see cref="FidelityReason.PropositionBindFailed"/> note per document that did not bind.</param>
    /// <returns>The layered source the rule binds against.</returns>
    public static ISpecSource Bind(
        IReadOnlyList<PinnedDocument> documents, ISpecSource live, PropositionSet propositions, List<FidelityNote> notes)
    {
        // Every name a pin named, parsed or not: a row that will not parse is still pinned, so its
        // dependents must wait on it rather than resolve through today's head.
        var pinnedNames = new HashSet<string>(documents.Select(document => document.Name), StringComparer.Ordinal);
        var pending = Parse(documents, propositions, notes);
        var overlay = new PropositionOverlay();
        var layered = new LayeredSpecSource(overlay, live);
        var bound = new HashSet<string>(StringComparer.Ordinal);

        // Bind whatever only waits on names that are bound already or not pinned at all; repeat
        // until a pass binds nothing. Whatever is left waits on a pin that failed, or on a cycle.
        while (pending.Count > 0)
        {
            var ready = pending.Where(IsReady).ToList();
            if (ready.Count == 0)
                break;

            foreach (var candidate in ready)
            {
                pending.Remove(candidate);
                if (BindOne(candidate, layered, propositions, notes) is { } entry)
                {
                    overlay.Set(entry);
                    bound.Add(candidate.Document.Name);
                }
            }
        }

        foreach (var left in pending)
        {
            var waitingOn = left.References.Where(r => pinnedNames.Contains(r) && !bound.Contains(r));
            notes.Add(new(FidelityReason.PropositionBindFailed,
                $"'{left.Document.Name}' v{left.Document.Version} was not bound: it references {string.Join(", ", waitingOn.Select(n => $"'{n}'"))}, which did not bind"));
        }

        return layered;

        bool IsReady(Pending candidate) =>
            candidate.References.All(name => bound.Contains(name) || !pinnedNames.Contains(name));
    }

    private sealed record Pending(PinnedDocument Document, RuleDocument Parsed, IReadOnlyList<string> References);

    /// <summary>Parses each document, noting the ones that will not parse and dropping them.</summary>
    private static List<Pending> Parse(IEnumerable<PinnedDocument> documents, PropositionSet propositions, List<FidelityNote> notes)
    {
        var parser = new RuleDocumentParser(propositions.Options);
        var pending = new List<Pending>();
        foreach (var document in documents)
        {
            var errors = new List<RuleError>();
            var parsed = document.DocumentJson is null ? null : parser.Parse(document.DocumentJson, errors);
            if (parsed is null || errors.Count > 0)
                notes.Add(new(FidelityReason.PropositionBindFailed, $"'{document.Name}' v{document.Version} does not parse: {Describe(errors)}"));
            else
                pending.Add(new Pending(document, parsed, DocumentReferences.From(parsed)));
        }

        return pending;
    }

    private static SpecRegistryEntry? BindOne(Pending candidate, ISpecSource source, PropositionSet propositions, List<FidelityNote> notes)
    {
        var errors = new List<RuleError>();
        var entry = propositions.ResolveModel(candidate.Document.ModelType, errors)?.Bind(
            source, candidate.Document.Name, candidate.Document.Description, candidate.Parsed,
            PropositionSet.BindsAsync(source, candidate.References), errors);
        if (entry is null)
            notes.Add(new(FidelityReason.PropositionBindFailed, $"'{candidate.Document.Name}' v{candidate.Document.Version} did not bind: {Describe(errors)}"));
        return entry;
    }

    private static string Describe(IReadOnlyList<RuleError> errors) =>
        errors.Count == 0 ? "no document" : string.Join("; ", errors.Select(error => error.Message));
}
