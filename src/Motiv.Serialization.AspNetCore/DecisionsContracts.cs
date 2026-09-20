using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A captured input over the wire: what kind of capture it was, and its value or its key.</summary>
/// <param name="Kind">The <see cref="DecisionInputKind"/> name.</param>
/// <param name="Value">The captured model or projection as the host's JSON, or null for a reference.</param>
/// <param name="Key">The reference key, for a reference-only capture.</param>
public sealed record DecisionInputEntry(string Kind, JsonElement? Value, string? Key);

/// <summary>A logged decision over the wire.</summary>
public sealed record DecisionEntry(
    Guid Id,
    string CorrelationId,
    DateTimeOffset TimestampUtc,
    string? Caller,
    string RuleName,
    int RuleVersion,
    string BuildId,
    IReadOnlyList<PropositionVersion> ReferencedPropositionVersions,
    DecisionInputEntry? Input,
    RuleEvaluationResult<object?> Outcome);

/// <summary>One way a reproduction fell short of exact.</summary>
public sealed record FidelityNoteEntry(string Reason, string Detail);

/// <summary>The fidelity verdict over the wire.</summary>
public sealed record FidelityEntry(bool IsExact, IReadOnlyList<FidelityNoteEntry> Notes);

/// <summary>The replayed model over the wire: the model as the host's JSON, or only its key.</summary>
/// <param name="Kind">The <see cref="ReproducedModelKind"/> name.</param>
public sealed record ReproducedModelEntry(string Kind, JsonElement? Value, string? Key);

/// <summary>A proposition version-log row over the wire.</summary>
public sealed record PropositionRowEntry(
    string Name, int Version, string? ModelType, JsonElement? Document, string? Description,
    string Author, DateTimeOffset TimestampUtc);

/// <summary>A rule version-log row over the wire.</summary>
public sealed record RuleRowEntry(
    string Name, int Version, JsonElement? Document, string Author, DateTimeOffset TimestampUtc,
    string? ChangeNote, string? ApprovalRef, string? BuildId);

/// <summary>A reproduction over the wire.</summary>
public sealed record ReproductionEntry(
    DecisionEntry Decision,
    RuleRowEntry? Rule,
    IReadOnlyList<PropositionRowEntry> Propositions,
    ReproducedModelEntry Model,
    RuleEvaluationResult<object?>? Replayed,
    FidelityEntry Fidelity,
    [property: JsonPropertyName("csharp")] string? CSharp,
    [property: JsonPropertyName("csharpWarnings")] IReadOnlyList<string> CSharpWarnings);

/// <summary>A rule printed as C#, and where the print needs a person.</summary>
public sealed record CSharpEntry(string Source, IReadOnlyList<string> Warnings);

/// <summary>
/// Projects the decision log's types onto their wire shapes. A model crosses only as the host's
/// JSON of what was captured or resolved; a reference crosses as its key and nothing else.
/// </summary>
internal static class DecisionsContracts
{
    public static DecisionEntry Entry(DecisionRecord record, JsonSerializerOptions json) =>
        new(record.Id, record.CorrelationId, record.TimestampUtc, record.Caller, record.RuleName, record.RuleVersion,
            record.BuildId, record.ReferencedPropositionVersions, Input(record.Input, json), record.Outcome);

    public static ReproductionEntry Entry(Reproduction reproduction, JsonSerializerOptions json) =>
        new(
            Entry(reproduction.Decision, json),
            reproduction.Rule is { } rule ? Entry(rule) : null,
            reproduction.Propositions.Select(Entry).ToArray(),
            Model(reproduction.Model, json),
            reproduction.Replayed,
            new FidelityEntry(
                reproduction.Fidelity.IsExact,
                reproduction.Fidelity.Notes.Select(note => new FidelityNoteEntry(note.Reason.ToString(), note.Detail)).ToArray()),
            reproduction.CSharp,
            reproduction.CSharpWarnings);

    public static RuleRowEntry Entry(StoredRuleVersion row) =>
        new(row.Name, row.Version, EndpointResponses.DocumentElement(row.DocumentJson), row.Author, row.TimestampUtc,
            row.ChangeNote, row.ApprovalRef, row.BuildId);

    public static PropositionRowEntry Entry(StoredPropositionVersion row) =>
        new(row.Name, row.Version, row.ModelType, EndpointResponses.DocumentElement(row.DocumentJson), row.Description,
            row.Author, row.TimestampUtc);

    private static DecisionInputEntry? Input(DecisionInput? input, JsonSerializerOptions json) => input switch
    {
        null => null,
        { Kind: DecisionInputKind.Reference } => new DecisionInputEntry(input.Kind.ToString(), null, (string?)input.Value),
        _ => new DecisionInputEntry(input.Kind.ToString(), Element(input.Value, json), null),
    };

    private static ReproducedModelEntry Model(ReproducedModel model, JsonSerializerOptions json) => model.Kind switch
    {
        ReproducedModelKind.Reference or ReproducedModelKind.Absent => new ReproducedModelEntry(model.Kind.ToString(), null, model.Key),
        _ => new ReproducedModelEntry(model.Kind.ToString(), Element(model.Value, json), model.Key),
    };

    private static JsonElement? Element(object? value, JsonSerializerOptions json) =>
        value is null ? null : value is JsonElement element ? element : JsonSerializer.SerializeToElement(value, json);
}
