namespace Motiv.Serialization;

internal sealed class RuleNode(RuleOperator @operator, string path)
{
    public RuleOperator Operator { get; } = @operator;

    public string Path { get; } = path;

    public string? SpecName { get; set; }

    public string? ExpressionText { get; set; }

    public List<RuleNode> Children { get; } = [];

    public string? WhenTrueText { get; set; }

    public string? WhenFalseText { get; set; }

    // Plan 2 replaces this flag with retained JsonElement payloads for typed metadata binding.
    public bool HasObjectPayloads { get; set; }

    public string? Name { get; set; }
}
