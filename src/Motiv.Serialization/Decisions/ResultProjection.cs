using Motiv.Shared;

namespace Motiv.Serialization;

/// <summary>
/// Projects a live <see cref="BooleanResultBase{TMetadata}"/> into the serialisable
/// <see cref="RuleEvaluationResult{TMetadata}"/> shape.
/// </summary>
/// <remarks>
/// Shared by <see cref="ResultSerializer"/>, which sends the projection over HTTP, and by the decision
/// log, which stores it. One projection rather than two, so the record of what a rule decided and the
/// response that reported it cannot describe the same evaluation differently.
/// </remarks>
internal static class ResultProjection
{
    /// <summary>Projects a result, keeping its metadata type.</summary>
    public static RuleEvaluationResult<TMetadata> Project<TMetadata>(BooleanResultBase<TMetadata> result) =>
        new(
            result.Satisfied,
            result.Reason,
            result.Assertions.ToArray(),
            result.Values.ToArray(),
            result.Justification,
            MapExplanation(result.Explanation));

    /// <summary>
    /// Projects a result, boxing its metadata. The decision log stores records of every rule in one
    /// place regardless of what each yields, so the stored payload cannot be generic in the rule's
    /// metadata type — and every posture that reads it back (JSON, a durable sink) is untyped anyway.
    /// </summary>
    public static RuleEvaluationResult<object?> ProjectUntyped<TMetadata>(BooleanResultBase<TMetadata> result) =>
        new(
            result.Satisfied,
            result.Reason,
            result.Assertions.ToArray(),
            [.. result.Values.Select(value => (object?)value)],
            result.Justification,
            MapExplanation(result.Explanation));

    private static ExplanationNode MapExplanation(Explanation explanation) =>
        new(
            explanation.Assertions.ToArray(),
            explanation.Underlying.Select(MapExplanation).ToArray());
}
