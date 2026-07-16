using System.Text.Json;

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

    // Cloned out of the parsed JsonDocument so they survive its disposal; deserialized to the
    // caller's TMetadata during a metadata load.
    public JsonElement? WhenTrueElement { get; set; }

    public JsonElement? WhenFalseElement { get; set; }

    public bool HasObjectPayloads => WhenTrueElement is not null;

    public string? Name { get; set; }

    public int? N { get; set; }

    public string? NParameterName { get; set; }

    public string? PathText { get; set; }
}
