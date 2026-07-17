using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Extension methods that mount the Motiv rules endpoints on an ASP.NET Core app.</summary>
public static class MotivRulesEndpoints
{
    /// <summary>
    /// Maps <c>GET {basePath}/catalog</c>, <c>POST {basePath}/validate</c>, and
    /// <c>POST {basePath}/evaluate</c>, backed by the given registry and options.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <param name="registry">The registry of specs documents may reference.</param>
    /// <param name="options">The endpoint options, including evaluable model registrations.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        SpecRegistry registry,
        MotivRulesOptions options)
    {
        var serializer = new RuleSerializer(registry, options.SerializerOptions);
        var resultSerializer = new ResultSerializer();
        var json = options.JsonSerializerOptions;
        var group = endpoints.MapGroup(basePath);

        group.MapGet("/catalog", () =>
        {
            var entries = registry.Entries
                .Select(entry => new CatalogEntry(
                    entry.Name,
                    options.ResolveModelId(entry.ModelType),
                    entry.MetadataType.Name,
                    entry.IsAsync,
                    entry.Description))
                .ToArray();
            return Results.Json(entries, json);
        });

        group.MapPost("/validate", (ValidateRequest request) =>
        {
            if (!options.TryGetBinding(request.ModelType, out var binding))
                return Results.Json(
                    new ErrorResponse($"Unknown model type '{request.ModelType}'."), json, statusCode: 400);

            var errors = binding.Validate(serializer, request.Document.GetRawText());
            return Results.Json(new ValidationResponse(errors), json);
        });

        return endpoints;
    }
}
