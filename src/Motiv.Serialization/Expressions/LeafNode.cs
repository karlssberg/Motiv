namespace Motiv.Serialization.Expressions;

/// <summary>A node of a parsed leaf; <c>Start</c>/<c>End</c> are offsets into the leaf text.</summary>
internal abstract record LeafNode(int Start, int End);

internal sealed record NumberLiteral(string Text, int Start, int End) : LeafNode(Start, End);
internal sealed record StringLiteral(string Value, int Start, int End) : LeafNode(Start, End);
internal sealed record BoolLiteral(bool Value, int Start, int End) : LeafNode(Start, End);
internal sealed record NullLiteral(int Start, int End) : LeafNode(Start, End);
internal sealed record ParameterRef(string Name, int Start, int End) : LeafNode(Start, End);
internal sealed record Identifier(string Name, int Start, int End) : LeafNode(Start, End);
internal sealed record MemberAccess(LeafNode Target, string Name, int NameStart, int NameEnd, int Start, int End) : LeafNode(Start, End);
internal sealed record MethodCall(LeafNode Target, string Method, IReadOnlyList<LeafNode> Arguments, int MethodStart, int MethodEnd, int Start, int End) : LeafNode(Start, End);
internal sealed record Lambda(string Parameter, LeafNode Body, int Start, int End) : LeafNode(Start, End);
internal sealed record Binary(string Operator, LeafNode Left, LeafNode Right, int Start, int End) : LeafNode(Start, End);
internal sealed record Unary(string Operator, LeafNode Operand, int Start, int End) : LeafNode(Start, End);
