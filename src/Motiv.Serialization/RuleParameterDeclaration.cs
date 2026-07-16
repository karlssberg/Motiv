namespace Motiv.Serialization;

internal sealed class RuleParameterDeclaration(
    string name,
    RuleParameterType type,
    bool hasDefault,
    object? defaultValue)
{
    public string Name { get; } = name;

    public RuleParameterType Type { get; } = type;

    public bool HasDefault { get; } = hasDefault;

    public object? DefaultValue { get; } = defaultValue;
}
