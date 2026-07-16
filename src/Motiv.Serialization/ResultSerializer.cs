using System.Text.Json;
using Motiv.Shared;

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

        return new RuleEvaluationResult<TMetadata>(
            result.Satisfied,
            result.Reason,
            result.Assertions.ToArray(),
            result.Values.ToArray(),
            result.Justification,
            MapExplanation(result.Explanation));
    }

    private static ExplanationNode MapExplanation(Explanation explanation) =>
        new(
            explanation.Assertions.ToArray(),
            explanation.Underlying.Select(MapExplanation).ToArray());
}
