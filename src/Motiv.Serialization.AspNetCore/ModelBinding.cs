using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Captures a registered evaluable model type <c>TModel</c> behind closures, so the endpoints can
/// validate and evaluate documents for that model without reflection.
/// </summary>
internal sealed class ModelBinding
{
    public required string Id { get; init; }

    public required Type ModelType { get; init; }

    /// <summary>Validates a raw rule-document JSON string, returning all errors (empty when valid).</summary>
    public required Func<RuleSerializer, string, IReadOnlyList<RuleError>> Validate { get; init; }

    /// <summary>
    /// Loads the document, binds the sample model element to <c>TModel</c>, evaluates, and projects
    /// the result. Throws <see cref="RuleSerializationException"/> when the document is invalid and
    /// <see cref="InvalidModelException"/> when the sample model cannot be bound.
    /// </summary>
    public required Func<RuleSerializer, ResultSerializer, JsonSerializerOptions, string, JsonElement, RuleEvaluationResult<string>> Evaluate { get; init; }
}

/// <summary>Thrown when a sample model element cannot be bound to the target model type.</summary>
internal sealed class InvalidModelException(string modelType)
    : Exception($"The supplied model could not be bound to model type '{modelType}'.")
{
    public string ModelType { get; } = modelType;
}
