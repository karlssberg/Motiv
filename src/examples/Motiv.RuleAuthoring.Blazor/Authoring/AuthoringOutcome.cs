namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>The result of authoring, validating and — when it is valid — evaluating a draft.</summary>
/// <param name="Json">The document that was written.</param>
/// <param name="Errors">The validation errors, each located on the draft node it concerns.</param>
/// <param name="Satisfied">The evaluation outcome, or <c>null</c> if the document did not validate.</param>
/// <param name="Reason">Motiv's one-line explanation, or <c>null</c> if the document did not validate.</param>
/// <param name="Justification">Motiv's hierarchical explanation, or <c>null</c> if the document did not validate.</param>
public sealed record AuthoringOutcome(
    string Json,
    IReadOnlyList<LocatedError> Errors,
    bool? Satisfied,
    string? Reason,
    string? Justification)
{
    /// <summary>An outcome for a document that did not validate.</summary>
    /// <param name="json">The document that was written.</param>
    /// <param name="errors">The errors, each located on the draft node it concerns.</param>
    /// <returns>The outcome.</returns>
    public static AuthoringOutcome Invalid(string json, IReadOnlyList<LocatedError> errors) =>
        new(json, errors, null, null, null);

    /// <summary>An outcome for a document that validated and was evaluated.</summary>
    /// <param name="json">The document that was written.</param>
    /// <param name="satisfied">The evaluation outcome.</param>
    /// <param name="reason">Motiv's one-line explanation.</param>
    /// <param name="justification">Motiv's hierarchical explanation.</param>
    /// <returns>The outcome.</returns>
    public static AuthoringOutcome Evaluated(
        string json,
        bool satisfied,
        string reason,
        string justification) =>
        new(json, [], satisfied, reason, justification);
}
