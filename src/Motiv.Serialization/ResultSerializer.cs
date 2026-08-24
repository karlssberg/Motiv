using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>
/// Projects Motiv evaluation results (<see cref="BooleanResultBase{TMetadata}"/>) into serializable
/// <see cref="RuleEvaluationResult{TMetadata}"/> documents, and renders them to JSON.
/// </summary>
public sealed class ResultSerializer
{
    private static readonly JsonSerializerOptions DefaultJsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>Creates a result serializer.</summary>
    /// <param name="jsonOptions">
    /// Options used when rendering to JSON (property naming and metadata <c>values</c> serialization).
    /// When omitted, camelCase property naming is used.
    /// </param>
    public ResultSerializer(JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? DefaultJsonOptions;
    }

    /// <summary>Projects an evaluation result into a serializable document.</summary>
    /// <typeparam name="TMetadata">The metadata type carried by the result.</typeparam>
    /// <param name="result">The evaluated result to project.</param>
    /// <returns>A serializable projection of <paramref name="result"/>.</returns>
    public RuleEvaluationResult<TMetadata> ToEvaluationResult<TMetadata>(BooleanResultBase<TMetadata> result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));

        return ResultProjection.Project(result);
    }

    /// <summary>Projects an evaluation result and renders it to a JSON string.</summary>
    /// <typeparam name="TMetadata">The metadata type carried by the result.</typeparam>
    /// <param name="result">The evaluated result to serialize.</param>
    /// <returns>The JSON representation of the projected result.</returns>
    public string Serialize<TMetadata>(BooleanResultBase<TMetadata> result) =>
        JsonSerializer.Serialize(ToEvaluationResult(result), _jsonOptions);
}
