using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Extension methods that mount the Motiv rules endpoints on an ASP.NET Core app.</summary>
public static class MotivRulesEndpoints
{
    /// <summary>
    /// Maps <c>GET {basePath}/catalog</c>, <c>POST {basePath}/validate</c>, and
    /// <c>POST {basePath}/evaluate</c>, backed by the given registry and options. When a
    /// <see cref="RuleSet"/> is supplied, also maps <c>GET {basePath}/rules</c>,
    /// <c>GET {basePath}/rules/{{name}}</c>, <c>PUT {basePath}/rules/{{name}}</c>, and
    /// <c>DELETE {basePath}/rules/{{name}}</c> for live rule management with optimistic concurrency.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <param name="registry">The registry of specs documents may reference.</param>
    /// <param name="options">The endpoint options, including evaluable model registrations.</param>
    /// <param name="rules">The live rule set to manage, or null to omit the rule endpoints.
    /// Construct it with the same registry and <see cref="MotivRulesOptions.SerializerOptions"/>
    /// passed here, so validate/evaluate and rule updates agree on how documents bind.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        SpecRegistry registry,
        MotivRulesOptions options,
        RuleSet? rules = null)
    {
        var serializer = new RuleSerializer(registry, options.SerializerOptions);
        var resultSerializer = new ResultSerializer();
        var json = options.JsonSerializerOptions;
        var group = endpoints.MapGroup(basePath);

        var specs = registry.Entries
            .Select(entry => new CatalogEntry(
                entry.Name,
                options.ResolveModelId(entry.ModelType),
                entry.MetadataType.Name,
                entry.IsAsync,
                entry.Description))
            .ToArray();

        var collections = registry.Collections
            .Select(collection => new CatalogCollection(
                collection.Path,
                options.ResolveModelId(collection.ParentType),
                options.ResolveModelId(collection.ElementType)))
            .ToArray();

        var catalog = new CatalogResponse(specs, collections);

        group.MapGet("/catalog", () => Results.Json(catalog, json));

        group.MapPost("/validate", (ValidateRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return MissingDocument(json);

            if (!options.TryGetBinding(request.ModelType, out var binding))
                return UnknownModelType(request.ModelType, json);

            var errors = binding.Validate(serializer, request.Document.GetRawText());
            return Results.Json(new ValidationResponse(errors), json);
        });

        group.MapPost("/evaluate", (EvaluateRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return MissingDocument(json);

            if (request.Model.ValueKind == JsonValueKind.Undefined)
                return Results.Json(
                    new ErrorResponse("The request must include a model."), json, statusCode: 400);

            if (!options.TryGetBinding(request.ModelType, out var binding))
                return UnknownModelType(request.ModelType, json);

            try
            {
                var result = binding.Evaluate(
                    serializer, resultSerializer, json, request.Document.GetRawText(), request.Model);
                return Results.Json(result, json);
            }
            catch (RuleSerializationException ex)
            {
                return Results.Json(new ValidationResponse(ex.Errors), json, statusCode: 400);
            }
            catch (InvalidModelException ex)
            {
                return Results.Json(new ErrorResponse(ex.Message), json, statusCode: 400);
            }
        });

        if (rules is not null)
            MapRuleEndpoints(group, rules, options, json);

        return endpoints;
    }

    /// <summary>
    /// Maps the rules endpoints using the registry, options, and <see cref="RuleSet"/> registered
    /// via <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/> — the RuleSet is
    /// guaranteed to share that registry and serializer options, so validate/evaluate and rule
    /// updates cannot diverge. Resolves the RuleSet eagerly so an invalid rule default fails
    /// here, at startup, rather than at first request.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(this IEndpointRouteBuilder endpoints, string basePath)
    {
        var services = endpoints.ServiceProvider;
        if (services.GetService<SpecRegistry>() is not { } registry
            || services.GetService<MotivRulesOptions>() is not { } options)
        {
            throw new InvalidOperationException(
                "The Motiv rules services are not registered. " +
                "Call services.AddMotivRules(registry, options) before MapMotivRules(basePath).");
        }

        // Resolving the RuleSet binds every enrolled rule's default — an invalid default
        // fails here, at startup, rather than at first request.
        return endpoints.MapMotivRules(basePath, registry, options, services.GetRequiredService<RuleSet>());
    }

    private static void MapRuleEndpoints(RouteGroupBuilder group, RuleSet rules, MotivRulesOptions options, JsonSerializerOptions json)
    {
        group.MapGet("/rules", () =>
            Results.Json(rules.Rules
                .Select(rule => new RuleListEntry(
                    rule.Name,
                    options.ResolveModelId(rule.ModelType),
                    rule.MetadataType.Name,
                    rule.IsAsync,
                    rule.IsPolicy,
                    rule.Version,
                    rule.Description))
                .ToArray(), json));

        group.MapGet("/rules/{name}", (string name) =>
        {
            // FindEntry serves document and version from a single coherent snapshot.
            if (rules.FindEntry(name) is not { } entry)
                return Results.Json(new ErrorResponse($"Unknown rule '{name}'."), json, statusCode: 404);

            JsonElement? document = null;
            if (entry.DocumentJson is not null)
            {
                using var parsed = JsonDocument.Parse(entry.DocumentJson);
                document = parsed.RootElement.Clone();
            }

            return Results.Json(new RuleGetResponse(document, entry.Version), json);
        });

        group.MapPut("/rules/{name}", (string name, RulePutRequest request) =>
        {
            if (request.Document.ValueKind == JsonValueKind.Undefined)
                return MissingDocument(json);

            if (request.BaseVersion <= 0)
                return NonPositiveBaseVersion(json);

            return ToResult(rules.Update(name, request.Document.GetRawText(), request.BaseVersion), name, json);
        });

        group.MapDelete("/rules/{name}", (string name, int baseVersion) =>
            baseVersion <= 0
                ? NonPositiveBaseVersion(json)
                : ToResult(rules.Revert(name, baseVersion), name, json));
    }

    private static IResult ToResult(RuleUpdateResult outcome, string name, JsonSerializerOptions json) =>
        outcome.Outcome switch
        {
            RuleUpdateOutcome.Updated => Results.Json(new RulePutResponse(outcome.Version), json),
            RuleUpdateOutcome.VersionConflict => Results.Json(new RuleConflictResponse(outcome.Version), json, statusCode: 409),
            RuleUpdateOutcome.Invalid => Results.Json(new ValidationResponse(outcome.Errors), json, statusCode: 400),
            _ => Results.Json(new ErrorResponse($"Unknown rule '{name}'."), json, statusCode: 404)
        };

    private static IResult UnknownModelType(string modelType, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown model type '{modelType}'."), json, statusCode: 400);

    private static IResult MissingDocument(JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse("The request must include a document."), json, statusCode: 400);

    private static IResult NonPositiveBaseVersion(JsonSerializerOptions json) =>
        Results.Json(
            new ErrorResponse("baseVersion must be a positive integer; versions start at 1."),
            json, statusCode: 400);
}
