using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A catalog listing for one registered specification.</summary>
/// <param name="Name">The stable name documents reference the spec by.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the spec evaluates asynchronously.</param>
/// <param name="Description">An optional human-readable description.</param>
public sealed record CatalogEntry(string Name, string ModelType, string MetadataType, bool IsAsync, string? Description);

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
