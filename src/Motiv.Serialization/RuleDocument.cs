namespace Motiv.Serialization;

internal sealed class RuleDocument(
    string? name,
    RuleNode? root,
    IReadOnlyList<RuleParameterDeclaration> parameters)
{
    public string? Name { get; } = name;

    public RuleNode? Root { get; } = root;

    public IReadOnlyList<RuleParameterDeclaration> Parameters { get; } = parameters;
}
