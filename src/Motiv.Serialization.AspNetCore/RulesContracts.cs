using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A catalog listing for one registered specification.</summary>
/// <param name="Name">The stable name documents reference the spec by.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the spec evaluates asynchronously.</param>
/// <param name="Description">An optional human-readable description.</param>
public sealed record CatalogEntry(string Name, string ModelType, string MetadataType, bool IsAsync, string? Description);

/// <summary>A catalog listing for one registered collection projection.</summary>
/// <param name="Path">The path higher-order nodes reference the collection by.</param>
/// <param name="ParentModelType">The registered model-type id the collection is selected from.</param>
/// <param name="ElementModelType">The registered model-type id of the collection's elements.</param>
public sealed record CatalogCollection(string Path, string ParentModelType, string ElementModelType);

/// <summary>The full catalog: registered specs and collections.</summary>
public sealed record CatalogResponse(IReadOnlyList<CatalogEntry> Specs, IReadOnlyList<CatalogCollection> Collections);

/// <summary>A request to validate a rule document against a registered model type.</summary>
/// <param name="ModelType">A model-type id registered on the server.</param>
/// <param name="Document">A rule document (see rule.v1.json).</param>
public sealed record ValidateRequest(string ModelType, JsonElement Document);

/// <summary>A request to evaluate a rule document against a sample model.</summary>
/// <param name="ModelType">A model-type id registered on the server.</param>
/// <param name="Document">A rule document (see rule.v1.json).</param>
/// <param name="Model">A sample model instance to evaluate against.</param>
public sealed record EvaluateRequest(string ModelType, JsonElement Document, JsonElement Model);

/// <summary>The outcome of a validation request.</summary>
/// <param name="Errors">All errors found; empty when the document is valid.</param>
public sealed record ValidationResponse(IReadOnlyList<RuleError> Errors);

/// <summary>A simple error envelope for request-level failures (e.g. unknown model type).</summary>
/// <param name="Error">A human-readable description of the failure.</param>
public sealed record ErrorResponse(string Error);

/// <summary>A listing of one live rule.</summary>
/// <param name="Name">The stable rule name.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the rule evaluates asynchronously.</param>
/// <param name="IsPolicy">Whether the rule yields a single value.</param>
/// <param name="Version">The current version.</param>
/// <param name="Description">An optional human-readable description.</param>
public sealed record RuleListEntry(
    string Name, string ModelType, string MetadataType, bool IsAsync, bool IsPolicy, int Version, string? Description);

/// <summary>One rule's current document and version.</summary>
/// <param name="Document">The current rule document, or null while on a compiled (code-defined) default.</param>
/// <param name="Version">The current version; pass it back as <c>baseVersion</c> when updating.</param>
public sealed record RuleGetResponse(JsonElement? Document, int Version);

/// <summary>A request to replace a rule's implementation.</summary>
/// <param name="Document">The replacement rule document.</param>
/// <param name="BaseVersion">The version the caller last observed; a stale value yields 409.</param>
public sealed record RulePutRequest(JsonElement Document, int BaseVersion);

/// <summary>A successful update or revert.</summary>
/// <param name="Version">The rule's new version.</param>
public sealed record RulePutResponse(int Version);

/// <summary>A rejected update: the caller's base version was stale.</summary>
/// <param name="CurrentVersion">The version the rule is actually at; reload before retrying.</param>
public sealed record RuleConflictResponse(int CurrentVersion);
