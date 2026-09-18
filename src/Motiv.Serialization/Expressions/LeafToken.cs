namespace Motiv.Serialization.Expressions;

internal enum LeafTokenKind { Identifier, Parameter, Number, String, Keyword, Operator, Punctuation, End }

/// <summary>One token of a leaf expression; offsets are into the leaf text, not the document.</summary>
internal sealed record LeafToken(LeafTokenKind Kind, string Text, int Start, int End);

/// <summary>A problem found in a leaf, at a range of the leaf text.</summary>
internal sealed record LeafProblem(RuleErrorCode Code, string Message, int Start, int End, bool IsWarning = false);
