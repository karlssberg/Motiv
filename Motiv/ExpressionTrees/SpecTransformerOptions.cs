namespace Motiv.ExpressionTrees;

internal record ExpressionTreeTransformerOptions
{
    internal string EqualsToken { get; set; } = "==";
    internal string NotEqualsToken { get; set; } = "!=";
    internal string GreaterThanToken { get; set; } = ">";
    internal string GreaterThanOrEqualToken { get; set; } = ">=";
    internal string LessThanToken { get; set; } = "<";
    internal string LessThanOrEqualToken { get; set; } = "<=";
}
