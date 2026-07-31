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
        RouteGroupBuilder group, PropositionSet propositions, JsonSerializerOptions json)
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

        group.MapPost("/propositions", (PropositionCreateRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.Json(new ErrorResponse("The request must include a name."), json, statusCode: 400);

            var result = propositions.Create(
                request.Name, request.ModelType, request.Document.GetRawText(), request.Description);

            return ToResult(result, request.Name, json);
        });

        group.MapPut("/propositions/{name}", (string name, PropositionPutRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return EndpointResponses.MissingDocument(json);
            if (request.BaseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            return ToResult(propositions.Update(name, request.Document.GetRawText(), request.BaseVersion), name, json);
        });

        group.MapDelete("/propositions/{name}", (string name, int baseVersion) =>
            baseVersion <= 0
                ? EndpointResponses.NonPositiveBaseVersion(json)
                : ToResult(propositions.Withdraw(name, baseVersion), name, json));

        group.MapGet("/propositions/{name}/dependents", (string name) =>
            propositions.Find(name) is null
                ? Unknown(name, json)
                : Results.Json(new DependentsResponse(
                    [.. propositions.Dependents(name)
                        .Select(dependent => new DependentEntry(dependent.Name, dependent.Kind))]), json));
    }

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
            _ => Results.Json(
                new CascadeFailureResponse(result.Errors, result.BrokenDependents), json, statusCode: 400)
        };

    private static IResult Unknown(string name, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown proposition '{name}'."), json, statusCode: 404);
}
