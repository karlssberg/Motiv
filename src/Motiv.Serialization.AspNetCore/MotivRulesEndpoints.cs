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
            if (!options.TryGetBinding(request.ModelType, out var binding))
                return UnknownModelType(request.ModelType, json);

            var errors = binding.Validate(serializer, request.Document.GetRawText());
            return Results.Json(new ValidationResponse(errors), json);
        });

        group.MapPost("/evaluate", (EvaluateRequest request) =>
        {
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

        return endpoints;
    }

    private static IResult UnknownModelType(string modelType, JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse($"Unknown model type '{modelType}'."), json, statusCode: 400);
}
