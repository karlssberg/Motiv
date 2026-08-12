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
public sealed class RuleParameterDeclaration
{
    /// <summary>Declares a parameter.</summary>
    /// <param name="name">The parameter name values are supplied under.</param>
    /// <param name="type">The scalar type a supplied value must coerce to.</param>
    /// <param name="hasDefault">Whether the parameter may be omitted in favour of <paramref name="defaultValue" />.</param>
    /// <param name="defaultValue">
    /// The value used when the parameter is omitted; meaningful only when <paramref name="hasDefault" />
    /// is <c>true</c>, and then held as the CLR type <paramref name="type" /> implies — an
    /// <see cref="RuleParameterType.Integer" /> default given as a <see cref="long" /> is narrowed to
    /// an <see cref="int" />, so a consumer's cast cannot disagree with the declaration.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="hasDefault" /> is <c>true</c> but <paramref name="defaultValue" /> is not a
    /// value of the declared type. Rejected here rather than at evaluation: a default that no
    /// document can override would otherwise reach the consuming code on every single evaluation,
    /// and fail there as a cast, long after the registration that caused it.
    /// </exception>
    public RuleParameterDeclaration(string name, RuleParameterType type, bool hasDefault, object? defaultValue)
    {
        Name = name;
        Type = type;
        HasDefault = hasDefault;
        DefaultValue = hasDefault ? CoerceDefault(name, type, defaultValue) : defaultValue;
    }

    /// <summary>The parameter name values are supplied under.</summary>
    public string Name { get; }

    /// <summary>The scalar type a supplied value must coerce to.</summary>
    public RuleParameterType Type { get; }

    /// <summary>Whether the parameter may be omitted in favour of <see cref="DefaultValue" />.</summary>
    public bool HasDefault { get; }

    /// <summary>The value used when the parameter is omitted; meaningful only when <see cref="HasDefault" />.</summary>
    public object? DefaultValue { get; }

    private static object CoerceDefault(string name, RuleParameterType type, object? defaultValue) =>
        RuleParameterResolver.Coerce(type, defaultValue)
        ?? throw new ArgumentException(
            $"The default declared for the parameter '{name}' is " +
            $"{(defaultValue is null ? "null" : $"a '{defaultValue.GetType().Name}'")}, which is not a " +
            $"'{type.ToString().ToLowerInvariant()}' value.", nameof(defaultValue));
}
