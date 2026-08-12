using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// The six proposition endpoints. Kept beside rather than inside
/// <see cref="MotivRulesEndpoints"/>, which is already long enough that adding a second CRUD surface
/// to it would bury both.
/// </summary>
internal static class MotivPropositionEndpoints
{
    internal static void MapPropositionEndpoints(
        RouteGroupBuilder group,
        PropositionSet propositions,
        ChangeRequestSet? governance,
        JsonSerializerOptions json)
    {
        group.MapGet("/propositions", () =>
            Results.Json(propositions.Propositions
                .Select(entry => new PropositionListEntry(
                    entry.Name, entry.ModelType, entry.MetadataType, entry.IsAsync,
                    entry.Origin.ToString(), entry.Version, entry.Description, entry.Quarantine))
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray(), json));

        group.MapGet("/propositions/{name}", (string name) =>
        {
            if (propositions.Find(name) is not { } entry)
                return Unknown(name, json);

            return Results.Json(new PropositionGetResponse(
                EndpointResponses.DocumentElement(propositions.DocumentJsonOf(name)),
                entry.Version,
                entry.Origin.ToString(),
                entry.Origin != PropositionOrigin.Authored), json);
        });

        group.MapPost("/propositions", (PropositionCreateRequest request, HttpContext http) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.Json(new ErrorResponse("The request must include a name."), json, statusCode: 400);

            if (GrantGate.Refuse(http, GrantVerb.Publish, request.Name, json) is { } refusal)
                return refusal;

            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);
            if (string.IsNullOrWhiteSpace(request.ModelType))
                return Results.Json(
                    new ErrorResponse("The request must include a modelType."), json, statusCode: 400);

            var documentJson = request.Document.GetRawText();

            // Grants first, exactly as before; then the governed publish, which answers only when it
            // published or the gate refused. Anything else falls through to the ungoverned write,
            // which refuses in the terms this endpoint has always refused in — a name-taken 409, a
            // cascade's broken dependents — that a change request cannot restate.
            return Governed(
                       governance, http, json, request.Name, documentJson,
                       baseVersion: 0, request.ModelType, created: true)
                   ?? ToResult(
                       propositions.Create(request.Name, request.ModelType, documentJson, request.Description),
                       request.Name, json);
        });

        group.MapPut("/propositions/{name}", (string name, PropositionPutRequest request, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Publish, name, json) is { } refusal)
                return refusal;

            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);
            if (request.BaseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            var documentJson = request.Document.GetRawText();

            return Governed(
                       governance, http, json, name, documentJson,
                       request.BaseVersion, modelTypeId: null, created: false)
                   ?? ToResult(propositions.Update(name, documentJson, request.BaseVersion), name, json);
        });

        group.MapDelete("/propositions/{name}", (string name, int baseVersion, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Publish, name, json) is { } refusal)
                return refusal;

            if (baseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            return Governed(
                       governance, http, json, name, documentJson: null,
                       baseVersion, modelTypeId: null, created: false)
                   ?? ToResult(propositions.Withdraw(name, baseVersion), name, json);
        });

        group.MapGet("/propositions/{name}/dependents", (string name) =>
            propositions.Find(name) is null
                ? Unknown(name, json)
                : Results.Json(new DependentsResponse(
                    [.. propositions.Dependents(name)
                        .Select(dependent => new DependentEntry(dependent.Name, dependent.Kind))]), json));
    }

    /// <summary>
    /// The proposition surface's half of the no-bypass wiring: publishes the write as a one-change
    /// request through the approval gate, or returns null to let the caller write directly.
    /// </summary>
    /// <param name="created">
    /// Whether a successful publish is a creation, which alone answers 201 — the change request
    /// reports a version, not which of the three writes produced it.
    /// </param>
    private static IResult? Governed(
        ChangeRequestSet? governance,
        HttpContext http,
        JsonSerializerOptions json,
        string name,
        string? documentJson,
        int baseVersion,
        string? modelTypeId,
        bool created) =>
        MotivGovernanceEndpoints.PublishDirectWrite(
            governance, http, json, ChangeTargetKind.Proposition, name, documentJson, baseVersion,
            modelTypeId,
            version => Results.Json(
                new PropositionSaveResponse(version), json, statusCode: created ? 201 : 200));

    /// <summary>
    /// The HTTP response for one attempted write. A single mapping serves create, update and
    /// withdraw: each reaches only its own success outcome — <see cref="PropositionSet.Create"/>
    /// alone reports Created, and so on — while every rejection is answered in the same terms
    /// whichever write provoked it.
    /// </summary>
    private static IResult ToResult(PropositionUpdateResult result, string name, JsonSerializerOptions json) =>
        result.Outcome switch
        {
            PropositionUpdateOutcome.Created =>
                Results.Json(new PropositionSaveResponse(result.Version), json, statusCode: 201),
            // Removed carries version 0, the value a withdrawal reports.
            PropositionUpdateOutcome.Updated or PropositionUpdateOutcome.Removed =>
                Results.Json(new PropositionSaveResponse(result.Version), json),
            PropositionUpdateOutcome.VersionConflict =>
                Results.Json(new RuleConflictResponse(result.Version), json, statusCode: 409),
            PropositionUpdateOutcome.NameTaken =>
                Results.Json(new ErrorResponse($"A proposition is already authored under '{name}'."), json, statusCode: 409),
            PropositionUpdateOutcome.Referenced =>
                Results.Json(new PropositionReferencedResponse(result.Referrers), json, statusCode: 409),
            PropositionUpdateOutcome.NotFound => Unknown(name, json),
            PropositionUpdateOutcome.Invalid => Results.Json(
                new CascadeFailureResponse(result.Errors, result.BrokenDependents), json, statusCode: 400),
            _ => throw new UnreachableException(
                $"Unhandled {nameof(PropositionUpdateOutcome)}: {result.Outcome}")
        };

    private static IResult Unknown(string name, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown proposition '{name}'."), json, statusCode: 404);
}
