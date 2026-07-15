namespace Motiv.Serialization;

/// <summary>Thrown when a rule document cannot be deserialized into a spec.</summary>
public sealed class RuleSerializationException : Exception
{
    /// <summary>Creates the exception from the errors found in the document.</summary>
    /// <param name="errors">The errors found in the document.</param>
    public RuleSerializationException(IReadOnlyList<RuleError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>All errors found in the document.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<RuleError> errors) =>
        errors.Count switch
        {
            0 => "The rule document is invalid.",
            1 => errors[0].ToString(),
            _ => $"{errors[0]} (+{errors.Count - 1} more)"
        };
}
