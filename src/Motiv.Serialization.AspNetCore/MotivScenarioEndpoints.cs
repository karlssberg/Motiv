using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// A rule's scenarios: listed under <see cref="GrantVerb.Read"/>, written under
/// <see cref="GrantVerb.Author"/>, and served by the store rather than the rule set — a scenario
/// may precede its rule's first publish. Ids beginning with <c>__</c> are reserved for host
/// bookkeeping (Studio's seed marker) and are never listed.
/// </summary>
internal static class MotivScenarioEndpoints
{
    private const string ReservedPrefix = "__";

    internal static void MapScenarioEndpoints(RouteGroupBuilder group, IScenarioStore scenarios, JsonSerializerOptions json)
    {
        group.MapGet("/rules/{name}/scenarios", async (string name, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Read, name, json) is { } refusal)
                return refusal;

            var rows = await scenarios.ForRuleAsync(name, http.RequestAborted);
            return Results.Json(
                rows.Where(row => !row.Id.StartsWith(ReservedPrefix, StringComparison.Ordinal))
                    .Select(ToEntry)
                    .ToArray(),
                json);
        });

        group.MapPut("/rules/{name}/scenarios/{id}", async (string name, string id, ScenarioPutRequest request, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Author, name, json) is { } refusal)
                return refusal;

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.Json(new ErrorResponse("The request must include a name."), json, statusCode: 400);
            if (request.Model is null)
                return Results.Json(new ErrorResponse("The request must include a model."), json, statusCode: 400);
            if (request.BaseVersion < 0)
                return Results.Json(
                    new ErrorResponse("baseVersion must be 0 to create, or the version last observed."), json, statusCode: 400);

            var scenario = new StoredScenario(
                name, id, request.Name, request.Model, request.ExpectedSatisfied, request.SourceDecisionId,
                Version: 0, PrincipalIdentity.Subject(http.User), DateTimeOffset.MinValue);
            return ToResult(await scenarios.PutAsync(scenario, request.BaseVersion, http.RequestAborted), json);
        });

        group.MapDelete("/rules/{name}/scenarios/{id}", async (string name, string id, int baseVersion, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Author, name, json) is { } refusal)
                return refusal;

            if (baseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            return ToResult(await scenarios.DeleteAsync(name, id, baseVersion, http.RequestAborted), json);
        });
    }

    private static ScenarioEntry ToEntry(StoredScenario row) =>
        new(row.Id, row.Name, row.ModelJson, row.ExpectedSatisfied, row.SourceDecisionId,
            row.Version, row.Author, row.TimestampUtc);

    private static IResult ToResult(ScenarioWriteResult written, JsonSerializerOptions json) =>
        written.IsConflict
            ? Results.Json(new ScenarioConflictResponse(written.CurrentVersion), json, statusCode: 409)
            : Results.Json(new ScenarioSaveResponse(written.Version), json);
}
