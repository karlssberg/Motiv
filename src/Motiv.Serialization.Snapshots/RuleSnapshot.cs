using System.Text.Json;

namespace Motiv.Serialization.Snapshots;

/// <summary>
/// A rule document and the proposition rows it resolved through, as a decision reproduction or
/// the <c>get_rule</c> tool handed them back — bound against a compiled registry the way the
/// reproduction bound them: the rows shadow whatever the registry has moved on to, in dependency
/// order, and everything else resolves through the compiled specs. What a generated test depends
/// on instead of a store or a host.
/// </summary>
public sealed class RuleSnapshot
{
    private readonly string _rule;
    private readonly IReadOnlyList<PinnedDocument> _propositions;

    private RuleSnapshot(string rule, IReadOnlyList<PinnedDocument> propositions)
    {
        _rule = rule;
        _propositions = propositions;
    }

    /// <summary>Every proposition row the last bind could not honour, with why. Empty after an exact bind.</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    /// <summary>
    /// Reads a snapshot. Each proposition is a version-log row as the reproduction or
    /// <c>get_rule</c> serialises it: <c>name</c>, <c>version</c>, <c>modelType</c>, <c>document</c>
    /// (an object, or the document as a string) and an optional <c>description</c>.
    /// </summary>
    /// <exception cref="ArgumentException">A row lacks a name or a document.</exception>
    public static RuleSnapshot FromJson(string rule, IEnumerable<string> propositions)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        if (propositions is null) throw new ArgumentNullException(nameof(propositions));

        return new RuleSnapshot(rule, propositions.Select(Row).ToList());
    }

    /// <summary>Binds the snapshot's rule over the registry, the rows shadowing it, and returns the spec.</summary>
    /// <exception cref="InvalidOperationException">The rows span more than one model type; a snapshot binds one model.</exception>
    /// <exception cref="RuleSerializationException">The rule itself does not bind.</exception>
    public SpecBase<TModel, string> Bind<TModel>(SpecRegistry registry, RuleSerializerOptions? options = null)
    {
        var serializer = Serializer<TModel>(registry, options);
        return serializer.Deserialize<TModel>(_rule);
    }

    /// <summary><see cref="Bind{TModel}"/> for a rule that composes async specs.</summary>
    public AsyncSpecBase<TModel, string> BindAsync<TModel>(SpecRegistry registry, RuleSerializerOptions? options = null)
    {
        var serializer = Serializer<TModel>(registry, options);
        return serializer.DeserializeAsyncSpec<TModel>(_rule);
    }

    private RuleSerializer Serializer<TModel>(SpecRegistry registry, RuleSerializerOptions? options)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        var serializerOptions = options ?? new RuleSerializerOptions();

        // One model per snapshot: the rows name model types by the host's ids, and the one CLR type
        // this bind knows is TModel. Rows over several ids would need the host's own registrations.
        var modelTypeIds = _propositions.Select(row => row.ModelType).Distinct(StringComparer.Ordinal).ToList();
        if (modelTypeIds.Count > 1)
            throw new InvalidOperationException(
                $"The snapshot's propositions span several model types ({string.Join(", ", modelTypeIds.Select(id => $"'{id}'"))}); a snapshot binds one model type, {typeof(TModel).Name}.");

        var bindings = modelTypeIds.ToDictionary(id => id ?? "", id => PropositionModelBinding.For<TModel>(id ?? "", serializerOptions), StringComparer.Ordinal);
        var notes = new List<FidelityNote>();
        var source = PinnedPropositionBinder.Bind(_propositions, registry, ResolveModel, serializerOptions, notes);
        Warnings = notes.Select(note => note.Detail).ToList();
        return new RuleSerializer(source, serializerOptions);

        PropositionModelBinding? ResolveModel(string? id, List<RuleError> errors)
        {
            if (bindings.TryGetValue(id ?? "", out var binding))
                return binding;
            errors.Add(new RuleError("$.modelType", RuleErrorCode.ModelTypeMismatch, $"model type '{id}' is not the snapshot's"));
            return null;
        }
    }

    private static PinnedDocument Row(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            throw new ArgumentException("A proposition row must be an object with a 'name'.", nameof(json));

        var version = root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
        var modelType = root.TryGetProperty("modelType", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        var description = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        string? document = null;
        if (root.TryGetProperty("document", out var doc))
            document = doc.ValueKind == JsonValueKind.String ? doc.GetString() : doc.ValueKind == JsonValueKind.Null ? null : doc.GetRawText();

        return new PinnedDocument(name.GetString()!, version, modelType, document, description);
    }
}
