namespace Motiv.Serialization;

/// <summary>
/// One declared parameter: its name, the scalar type a supplied value must coerce to, and an
/// optional default that stands in when no value is supplied.
/// </summary>
/// <remarks>
/// The same declaration serves both directions of parameterization — a document's own
/// <c>parameters</c> block, and the arguments a parameterised registry entry accepts from a
/// <c>spec</c> node's <c>args</c> — so both are validated by one set of rules rather than two.
/// </remarks>
/// <param name="name">The parameter name values are supplied under.</param>
/// <param name="type">The scalar type a supplied value must coerce to.</param>
/// <param name="hasDefault">Whether the parameter may be omitted in favour of <paramref name="defaultValue" />.</param>
/// <param name="defaultValue">
/// The value used when the parameter is omitted; meaningful only when <paramref name="hasDefault" />
/// is <c>true</c>.
/// </param>
public sealed class RuleParameterDeclaration(
    string name,
    RuleParameterType type,
    bool hasDefault,
    object? defaultValue)
{
    /// <summary>The parameter name values are supplied under.</summary>
    public string Name { get; } = name;

    /// <summary>The scalar type a supplied value must coerce to.</summary>
    public RuleParameterType Type { get; } = type;

    /// <summary>Whether the parameter may be omitted in favour of <see cref="DefaultValue" />.</summary>
    public bool HasDefault { get; } = hasDefault;

    /// <summary>The value used when the parameter is omitted; meaningful only when <see cref="HasDefault" />.</summary>
    public object? DefaultValue { get; } = defaultValue;
}
