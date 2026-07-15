namespace Motiv.Serialization;

/// <summary>A single validation or load error found in a rule document.</summary>
public sealed class RuleError
{
    /// <summary>Creates a rule error.</summary>
    /// <param name="path">The JSON path of the offending element, e.g. <c>$.rule.and[1].whenTrue</c>.</param>
    /// <param name="code">The stable machine-readable error code.</param>
    /// <param name="message">The human-readable description of the error.</param>
    public RuleError(string path, RuleErrorCode code, string message)
    {
        Path = path;
        Code = code;
        Message = message;
    }

    /// <summary>The JSON path of the offending element, e.g. <c>$.rule.and[1].whenTrue</c>.</summary>
    public string Path { get; }

    /// <summary>The stable machine-readable error code.</summary>
    public RuleErrorCode Code { get; }

    /// <summary>The human-readable description of the error.</summary>
    public string Message { get; }

    /// <summary>Formats the error as <c>Code at Path: Message</c>.</summary>
    /// <returns>The formatted error.</returns>
    public override string ToString() => $"{Code} at {Path}: {Message}";
}
