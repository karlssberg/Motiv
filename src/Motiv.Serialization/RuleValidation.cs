namespace Motiv.Serialization;

/// <summary>What the checker learned about one place in an expression leaf: a literal or parameter's
/// solved type, a leaf root's result type, or a warning raised while checking it.</summary>
/// <param name="Path">The JSON path of the expression leaf this fact belongs to.</param>
/// <param name="Range">Where inside the leaf's text this fact's span lies.</param>
/// <param name="Text">The leaf text spanned by <see cref="Range" />.</param>
/// <param name="Type">The solved CLR type's short name, or empty for a warning fact.</param>
/// <param name="From">The anchor a resolved parameter/literal was found through, or <c>null</c>.</param>
/// <param name="IsWarning">Whether this fact reports a warning rather than a solved type.</param>
/// <param name="Message">The warning's message, or <c>null</c> for a non-warning fact.</param>
public sealed record RuleLeafFact(
    string Path, RuleTextRange Range, string Text, string Type, string? From, bool IsWarning, string? Message);

/// <summary>The outcome of inspecting a document: its errors, and the facts about its leaves.</summary>
/// <param name="Errors">All errors found, or an empty list when the document would load.</param>
/// <param name="Facts">The facts gathered about every expression leaf reached while checking the document.</param>
public sealed record RuleValidation(IReadOnlyList<RuleError> Errors, IReadOnlyList<RuleLeafFact> Facts);
