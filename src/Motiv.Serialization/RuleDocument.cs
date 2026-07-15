namespace Motiv.Serialization;

internal sealed class RuleDocument(string? name, RuleNode? root)
{
    public string? Name { get; } = name;

    public RuleNode? Root { get; } = root;
}
