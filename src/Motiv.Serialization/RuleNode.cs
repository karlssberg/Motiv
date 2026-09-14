using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class RuleNode(RuleOperator @operator, string path)
{
    public RuleOperator Operator { get; } = @operator;

    public string Path { get; } = path;

    public string? SpecName { get; set; }

    public string? ExpressionText { get; set; }

    /// <summary>The definition this node names, on a <see cref="RuleOperator.Local" /> node.</summary>
    public string? LocalName { get; set; }

    /// <summary>
    /// The definition body a <see cref="RuleOperator.Local" /> node resolves to, linked by
    /// <see cref="LocalResolver" /> once the whole envelope has parsed. Non-null on every local node
    /// of a document that parsed without errors, which is what lets a binder bind one by binding this.
    /// </summary>
    public RuleNode? Definition { get; set; }

    // Scalar arguments supplied to a parameterised registry entry, keyed by declared parameter
    // name. Null when the node supplied no 'args' at all, which is what distinguishes "took no
    // arguments" from "took an empty argument object".
    public Dictionary<string, object?>? Args { get; set; }

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
