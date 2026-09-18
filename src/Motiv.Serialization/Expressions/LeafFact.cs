namespace Motiv.Serialization.Expressions;

/// <summary>What the checker learned about a node: its solved CLR type and, for a literal or
/// parameter, the anchor that fixed it (null when it took the default).</summary>
internal sealed record LeafFact(LeafNode Node, Type Type, string? From);
