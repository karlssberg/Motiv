using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// The decision log read back — a page, one record, and one record reproduced — and a rule's
/// document printed as C#. Every read needs <see cref="GrantVerb.Read"/> on the rule concerned; a
/// page silently omits the records the caller may not read.
/// </summary>
internal static class MotivDecisionEndpoints
{
    private const int MaxLimit = 1000;

    internal static void MapDecisionEndpoints(
        RouteGroupBuilder group, IDecisionSource decisions, DecisionReproducer? reproducer, JsonSerializerOptions json)
    {
        group.MapGet("/decisions", async (
            HttpContext http, string? correlationId, string? ruleName, bool? satisfied,
            DateTimeOffset? from, DateTimeOffset? to, int? limit) =>
        {
            var query = new DecisionQuery
            {
                CorrelationId = correlationId, RuleName = ruleName, Satisfied = satisfied, FromUtc = from, ToUtc = to,
                Limit = Math.Clamp(limit ?? 100, 1, MaxLimit),
            };
            var records = await decisions.QueryAsync(query, http.RequestAborted);
            var readable = records
                .Where(record => GrantGate.IsGranted(http, GrantVerb.Read, record.RuleName))
                .Select(record => DecisionsContracts.Entry(record, json))
                .ToArray();
            return Results.Json(new { records = readable }, json);
        });

        group.MapGet("/decisions/{id:guid}", async (Guid id, HttpContext http) =>
        {
            if (await decisions.FindAsync(id, http.RequestAborted) is not { } record)
                return UnknownDecision(id, json);
            if (GrantGate.Refuse(http, GrantVerb.Read, record.RuleName, json) is { } refusal)
                return refusal;

            return Results.Json(DecisionsContracts.Entry(record, json), json);
        });

        group.MapGet("/decisions/{id:guid}/reproduction", async (Guid id, HttpContext http) =>
        {
            if (await decisions.FindAsync(id, http.RequestAborted) is not { } record)
                return UnknownDecision(id, json);
            if (GrantGate.Refuse(http, GrantVerb.Read, record.RuleName, json) is { } refusal)
                return refusal;
            if (reproducer is null)
                return Results.Json(new ErrorResponse("This host has no DecisionReproducer: call AddRuleStore() and AddPropositions() beside AddDecisionSource()."), json, statusCode: 503);

            var reproduction = await reproducer.ReproduceAsync(id, http.RequestAborted);
            return Results.Json(DecisionsContracts.Entry(reproduction, json), json);
        });
    }

    /// <summary>
    /// A rule's document at a version — the live one by default — printed as the reproducer prints
    /// it. A version that ran compiled code has no document and answers <c>204</c>.
    /// </summary>
    internal static void MapCSharpEndpoint(
        RouteGroupBuilder group, RuleSet rules, IRuleStore? store, RuleSerializerOptions serializerOptions, JsonSerializerOptions json)
    {
        group.MapGet("/rules/{name}/csharp", async (string name, int? version, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Read, name, json) is { } refusal)
                return refusal;
            if (rules.Find(name) is not { } rule || rules.FindEntry(name) is not { } live)
                return Results.Json(new ErrorResponse($"Unknown rule '{name}'."), json, statusCode: 404);

            var (exists, documentJson) = await RuleVersionLookup.DocumentAtAsync(rule, live, store, version, http.RequestAborted);
            if (!exists)
                return Results.Json(new ErrorResponse(store is null
                    ? $"Rule '{name}' has no version {version}: this host keeps no rule history."
                    : $"Rule '{name}' has no version {version}."), json, statusCode: 404);

            if (documentJson is null)
                return Results.NoContent();

            try
            {
                var printed = CSharpPrinter.Print(documentJson, RulePrintOptions.For(rule, rules.Scope.Registry, serializerOptions));
                return Results.Json(new CSharpEntry(printed.Source, printed.Warnings), json);
            }
            catch (RuleSerializationException exception)
            {
                return Results.Json(new ValidationResponse(exception.Errors), json, statusCode: 400);
            }
        });
    }

    private static IResult UnknownDecision(Guid id, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"No decision '{id}' is in the log; retention may have purged it."), json, statusCode: 404);
}
