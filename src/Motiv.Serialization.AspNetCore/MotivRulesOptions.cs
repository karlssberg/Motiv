using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Configures the Motiv rules endpoints: the evaluable models and JSON behavior.</summary>
public sealed class MotivRulesOptions
{
    private readonly Dictionary<string, ModelBinding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _idByType = new();

    /// <summary>
    /// JSON options used to read sample models and write all responses. Defaults to web (camelCase)
    /// conventions with enums serialized as their names (so error codes are strings).
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Options forwarded to the underlying <see cref="RuleSerializer"/>, or <c>null</c> for defaults.</summary>
    public RuleSerializerOptions? SerializerOptions { get; set; }

    /// <summary>Registers a model type as evaluable under a stable id used by the endpoints and catalog.</summary>
    /// <typeparam name="TModel">The model type documents evaluate against.</typeparam>
    /// <param name="id">The stable id clients pass as <c>modelType</c>.</param>
    /// <returns>This options instance, to allow chained registration.</returns>
    public MotivRulesOptions AddModel<TModel>(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A model id must not be empty or whitespace.", nameof(id));
        if (_bindings.ContainsKey(id))
            throw new ArgumentException($"A model is already registered under the id '{id}'.", nameof(id));

        _bindings[id] = new ModelBinding
        {
            Id = id,
            ModelType = typeof(TModel),
            Validate = static (serializer, documentJson) =>
            {
                var structural = serializer.Validate(documentJson);
                if (structural.Count > 0) return structural;
                try
                {
                    serializer.Deserialize<TModel>(documentJson);
                    return Array.Empty<RuleError>();
                }
                catch (RuleSerializationException ex)
                {
                    return ex.Errors;
                }
            },
            Evaluate = static (serializer, resultSerializer, jsonOptions, documentJson, modelElement) =>
            {
                var spec = serializer.Deserialize<TModel>(documentJson);
                TModel? model;
                try
                {
                    model = modelElement.Deserialize<TModel>(jsonOptions);
                }
                catch (JsonException)
                {
                    // A shape mismatch is a caller error, not a server fault.
                    throw new InvalidModelException(typeof(TModel).Name);
                }
                if (model is null) throw new InvalidModelException(typeof(TModel).Name);
                var result = spec.Evaluate(model);
                return resultSerializer.ToEvaluationResult(result);
            }
        };
        _idByType[typeof(TModel)] = id;
        return this;
    }

    internal bool TryGetBinding(string id, out ModelBinding binding) =>
        _bindings.TryGetValue(id, out binding!);

    /// <summary>Resolves a spec's model type to its registered id, falling back to the CLR type name.</summary>
    internal string ResolveModelId(Type modelType) =>
        _idByType.TryGetValue(modelType, out var id) ? id : modelType.Name;
}
