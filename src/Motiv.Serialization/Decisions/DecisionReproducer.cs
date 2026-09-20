using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>
/// Turns a logged decision into everything needed to run it again: the rule and every referenced
/// proposition at the version that decided, the model as far as capture and a resolver allow, a
/// replay under exactly those documents, and a fidelity verdict naming each anchor that slipped.
/// The live sets are read, never written; the overlay the pinned propositions bind into is
/// transient and layered over the live source, so a pinned version shadows today's head while
/// everything unpinned resolves as it does in production.
/// </summary>
/// <param name="decisions">Where the decision is read from.</param>
/// <param name="ruleStore">The rule version log the pinned rule version is read from.</param>
/// <param name="propositionStore">The proposition version log the pinned versions are read from.</param>
/// <param name="rules">The live rules, for the rule object itself and the source beneath the overlay.</param>
/// <param name="propositions">The live propositions, for the model bindings pinned documents bind through; null when the host has none.</param>
/// <param name="resolvers">How a reference-only capture gets its model back.</param>
/// <param name="modelJson">The options a captured model is rehydrated through — the host's own, so converters match.</param>
public sealed class DecisionReproducer(
    IDecisionSource decisions,
    IRuleStore ruleStore,
    IPropositionStore propositionStore,
    RuleSet rules,
    PropositionSet? propositions,
    DecisionModelResolvers resolvers,
    JsonSerializerOptions modelJson)
{
    /// <summary>Reproduces one logged decision.</summary>
    /// <exception cref="DecisionNotFoundException">The log holds no decision with that id.</exception>
    public async Task<Reproduction> ReproduceAsync(Guid decisionId, CancellationToken cancellationToken)
    {
        var decision = await decisions.FindAsync(decisionId, cancellationToken).ConfigureAwait(false)
            ?? throw new DecisionNotFoundException(decisionId);
        var notes = new List<FidelityNote>();

        if (decision.BuildId != BuildIdentity.Current)
            notes.Add(new(FidelityReason.BuildMismatch, $"decided on build '{decision.BuildId}'; this host is '{BuildIdentity.Current}'"));

        var rule = rules.Find(decision.RuleName);
        var ruleVersion = await PinnedRuleVersionAsync(decision, rule, notes, cancellationToken).ConfigureAwait(false);
        var (pinned, source) = await BindPinnedPropositionsAsync(decision.ReferencedPropositionVersions, notes, cancellationToken).ConfigureAwait(false);
        var model = rule is null
            ? new ReproducedModel(ReproducedModelKind.Absent, null, null)
            : await RehydrateAsync(decision.Input, rule.ModelType, notes, cancellationToken).ConfigureAwait(false);

        // A pinned document that did not bind leaves the rule to resolve that name through today's
        // head, and a verdict computed that way is not a reproduction of anything. No replay, then.
        var pinsBound = notes.All(note => note.Reason != FidelityReason.PropositionBindFailed);
        var replayed = rule is not null && ruleVersion is not null && model.Value is not null && pinsBound
            ? await ReplayAsync(rule, ruleVersion, source, model.Value, decision, notes, cancellationToken).ConfigureAwait(false)
            : null;

        var (csharp, csharpWarnings) = Print(rule, ruleVersion);
        return new Reproduction(decision, ruleVersion, pinned, model, replayed, new ReproductionFidelity(notes), csharp, csharpWarnings);
    }

    /// <summary>
    /// The pinned document as C#, with every compiled reference through the registry and every
    /// registered collection of the model named by its element type. A revert has no document and
    /// prints nothing; a document that will not print — it parsed for binding, so this is defensive —
    /// prints nothing and says why.
    /// </summary>
    private (string? Source, IReadOnlyList<string> Warnings) Print(RuleBase? rule, StoredRuleVersion? ruleVersion)
    {
        if (rule is null || ruleVersion?.DocumentJson is null)
            return (null, []);

        var registry = rules.Scope.Registry;
        var options = new CSharpPrintOptions
        {
            ModelType = rule.ModelType,
            AsyncSpecs = new HashSet<string>(registry.Entries.Where(entry => entry.IsAsync).Select(entry => entry.Name), StringComparer.Ordinal),
            KnownSpecs = new HashSet<string>(registry.Entries.Select(entry => entry.Name), StringComparer.Ordinal),
            Collections = registry.Collections
                .Where(collection => collection.ParentType == rule.ModelType)
                .ToDictionary(collection => collection.Path, collection => new CSharpCollectionHandle(collection.ElementType.Name, Selector: null), StringComparer.Ordinal),
            ClassName = CSharpIdentifiers.PascalCase(rule.Name) + "Rule",
            SerializerOptions = propositions?.Options ?? rules.Options,
        };

        try
        {
            var printed = CSharpPrinter.Print(ruleVersion.DocumentJson, options);
            return (printed.Source, printed.Warnings);
        }
        catch (RuleSerializationException exception)
        {
            return (null, [$"the pinned document did not print: {Describe(exception.Errors)}"]);
        }
    }

    private async Task<StoredRuleVersion?> PinnedRuleVersionAsync(
        DecisionRecord decision, RuleBase? rule, List<FidelityNote> notes, CancellationToken cancellationToken)
    {
        var history = await ruleStore.HistoryAsync(decision.RuleName, cancellationToken).ConfigureAwait(false);
        var row = history.FirstOrDefault(r => r.Version == decision.RuleVersion);

        if (rule is null)
            notes.Add(new(FidelityReason.RuleVersionMissing, $"no rule '{decision.RuleName}' is registered on this host"));
        else if (row is null)
            notes.Add(new(FidelityReason.RuleVersionMissing, $"'{decision.RuleName}' v{decision.RuleVersion} is not in the rule log"));

        return rule is null ? null : row;
    }

    private async Task<RuleEvaluationResult<object?>?> ReplayAsync(
        RuleBase rule, StoredRuleVersion ruleVersion, ISpecSource source, object model,
        DecisionRecord decision, List<FidelityNote> notes, CancellationToken cancellationToken)
    {
        try
        {
            var serializer = new RuleSerializer(source, propositions?.Options ?? rules.Options);
            var replayed = await rule.ReplayAsync(serializer, ruleVersion.DocumentJson, model, cancellationToken).ConfigureAwait(false);
            if (replayed.Satisfied != decision.Outcome.Satisfied)
                notes.Add(new(FidelityReason.OutcomeDiverged, $"the log says {Verdict(decision.Outcome.Satisfied)}; the replay says {Verdict(replayed.Satisfied)}"));
            return replayed;
        }
        catch (RuleSerializationException exception)
        {
            notes.Add(new(FidelityReason.PropositionBindFailed, $"'{rule.Name}' v{ruleVersion.Version} did not bind: {Describe(exception.Errors)}"));
            return null;
        }
    }

    private static string Verdict(bool satisfied) => satisfied ? "satisfied" : "not satisfied";

    private static string Describe(IReadOnlyList<RuleError> errors) =>
        string.Join("; ", errors.Select(error => error.Message));

    /// <summary>
    /// Reads every pinned proposition row and hands them to <see cref="BindIntoOverlay"/>, which
    /// binds them over the live source. A pin the log no longer holds falls back to the name's live
    /// head and says so; a host with no proposition set binds nothing and says that instead.
    /// </summary>
    private async Task<(IReadOnlyList<StoredPropositionVersion> Rows, ISpecSource Source)> BindPinnedPropositionsAsync(
        IReadOnlyList<PropositionVersion> pins, List<FidelityNote> notes, CancellationToken cancellationToken)
    {
        var live = rules.Scope.Source;
        if (pins.Count == 0)
            return ([], live);

        if (propositions is null)
        {
            foreach (var pin in pins)
                notes.Add(new(FidelityReason.PropositionBindFailed, $"'{pin.Name}' v{pin.Version}: this host has no proposition set to bind it through"));
            return ([], live);
        }

        var rows = new List<StoredPropositionVersion>();
        foreach (var pin in pins)
        {
            if (await PinnedRowAsync(pin, notes, cancellationToken).ConfigureAwait(false) is { } row)
                rows.Add(row);
        }

        // Every name a pin named, parsed or not: a row that will not parse is still pinned, so its
        // dependents must wait on it rather than resolve through today's head.
        var pinnedNames = new HashSet<string>(rows.Select(row => row.Name), StringComparer.Ordinal);
        return (rows, BindIntoOverlay(ParsePending(rows, notes), pinnedNames, live, notes));
    }

    /// <summary>
    /// Binds the parsed rows, in dependency order, into an overlay layered over
    /// <paramref name="live"/>, and returns that layered source. A document that will not bind is
    /// noted by <see cref="Bind"/> and its dependents are left unbound — never quietly resolved
    /// through today's head — as is anything caught in a reference cycle.
    /// </summary>
    private ISpecSource BindIntoOverlay(
        List<Pending> pending, HashSet<string> pinnedNames, ISpecSource live, List<FidelityNote> notes)
    {
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
                if (Bind(candidate, layered, notes) is { } entry)
                {
                    overlay.Set(entry);
                    bound.Add(candidate.Row.Name);
                }
            }
        }

        foreach (var left in pending)
        {
            var waitingOn = left.References.Where(r => pinnedNames.Contains(r) && !bound.Contains(r));
            notes.Add(new(FidelityReason.PropositionBindFailed,
                $"'{left.Row.Name}' v{left.Row.Version} was not bound: it references {string.Join(", ", waitingOn.Select(n => $"'{n}'"))}, which did not bind"));
        }

        return layered;

        bool IsReady(Pending candidate) =>
            candidate.References.All(name => bound.Contains(name) || !pinnedNames.Contains(name));
    }

    private async Task<StoredPropositionVersion?> PinnedRowAsync(
        PropositionVersion pin, List<FidelityNote> notes, CancellationToken cancellationToken)
    {
        var history = await propositionStore.HistoryAsync(pin.Name, cancellationToken).ConfigureAwait(false);
        if (history.FirstOrDefault(r => r.Version == pin.Version && !r.IsTombstone) is { } exact)
            return exact;

        // HeadOf answers the head proposition, not the row that carries it, so the row is looked up
        // by the version it reports rather than re-deriving "highest, unless tombstoned" here.
        var fallback = StoredPropositionVersion.HeadOf(history) is { } head
            ? history.First(r => r.Version == head.Version)
            : null;
        notes.Add(new(FidelityReason.PropositionVersionMissing, fallback is null
            ? $"'{pin.Name}' v{pin.Version} is not in the proposition log, and the name has no live head to stand in"
            : $"'{pin.Name}' v{pin.Version} is not in the proposition log; its head, v{fallback.Version}, was bound instead"));
        return fallback;
    }

    private sealed record Pending(StoredPropositionVersion Row, RuleDocument Document, IReadOnlyList<string> References);

    /// <summary>Parses each row's document, noting the ones that will not parse and dropping them.</summary>
    private List<Pending> ParsePending(IEnumerable<StoredPropositionVersion> rows, List<FidelityNote> notes)
    {
        var parser = new RuleDocumentParser(propositions!.Options);
        var pending = new List<Pending>();
        foreach (var row in rows)
        {
            var errors = new List<RuleError>();
            var document = parser.Parse(row.DocumentJson!, errors);
            if (document is null || errors.Count > 0)
                notes.Add(new(FidelityReason.PropositionBindFailed, $"'{row.Name}' v{row.Version} does not parse: {Describe(errors)}"));
            else
                pending.Add(new Pending(row, document, DocumentReferences.From(document)));
        }

        return pending;
    }

    private SpecRegistryEntry? Bind(Pending candidate, ISpecSource source, List<FidelityNote> notes)
    {
        var errors = new List<RuleError>();
        var entry = propositions!.ResolveModel(candidate.Row.ModelType, errors)?.Bind(
            source, candidate.Row.Name, candidate.Row.Description, candidate.Document,
            PropositionSet.BindsAsync(source, candidate.References), errors);
        if (entry is null)
            notes.Add(new(FidelityReason.PropositionBindFailed, $"'{candidate.Row.Name}' v{candidate.Row.Version} did not bind: {Describe(errors)}"));
        return entry;
    }

    /// <summary>
    /// The model the replay runs. A whole or redacted capture is rehydrated to the rule's model
    /// type — a <see cref="JsonElement"/> from a durable sink, or an in-memory object, either way
    /// through the host's own JSON options — and a reference goes through the registered resolver.
    /// </summary>
    private async Task<ReproducedModel> RehydrateAsync(
        DecisionInput? input, Type modelType, List<FidelityNote> notes, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            notes.Add(new(FidelityReason.ModelUnresolved, "nothing was captured for this decision"));
            return new ReproducedModel(ReproducedModelKind.Absent, null, null);
        }

        switch (input.Kind)
        {
            case DecisionInputKind.Reference:
                return await ResolveAsync((string)input.Value!, modelType, notes, cancellationToken).ConfigureAwait(false);
            case DecisionInputKind.Redacted:
                notes.Add(new(FidelityReason.ModelRedacted, "the capture was a projection; fields the rule reads may be absent"));
                return new ReproducedModel(ReproducedModelKind.Redacted, Rehydrate(input.Value, modelType, notes), null);
            case DecisionInputKind.Whole:
            default:
                return new ReproducedModel(ReproducedModelKind.Whole, Rehydrate(input.Value, modelType, notes), null);
        }
    }

    private async Task<ReproducedModel> ResolveAsync(
        string key, Type modelType, List<FidelityNote> notes, CancellationToken cancellationToken)
    {
        if (!resolvers.Covers(modelType))
        {
            notes.Add(new(FidelityReason.ModelUnresolved,
                $"no resolver is registered for {modelType.Name}; register one on DecisionLogOptions.Resolve to replay reference-only captures"));
            return new ReproducedModel(ReproducedModelKind.Reference, null, key);
        }

        var resolved = await resolvers.ResolveAsync(modelType, key, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            notes.Add(new(FidelityReason.ModelUnresolved, $"the resolver returned nothing for '{key}'; the subject may have been erased"));
            return new ReproducedModel(ReproducedModelKind.Reference, null, key);
        }

        return new ReproducedModel(ReproducedModelKind.Resolved, resolved, key);
    }

    private object? Rehydrate(object? value, Type modelType, List<FidelityNote> notes)
    {
        if (value is null)
        {
            notes.Add(new(FidelityReason.ModelUnresolved, "the captured model was null"));
            return null;
        }

        if (modelType.IsInstanceOfType(value))
            return value;

        try
        {
            var element = value is JsonElement captured ? captured : JsonSerializer.SerializeToElement(value, modelJson);
            return JsonSerializer.Deserialize(element, modelType, modelJson);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            notes.Add(new(FidelityReason.ModelUnresolved, $"the captured model could not be read as {modelType.Name}: {exception.Message}"));
            return null;
        }
    }
}
