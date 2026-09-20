namespace Motiv.Serialization;

/// <summary>A half-open character range <c>[Start, End)</c> inside an expression leaf's text.</summary>
public sealed record RuleTextRange(int Start, int End);
