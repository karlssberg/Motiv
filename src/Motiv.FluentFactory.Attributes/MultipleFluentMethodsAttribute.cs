using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Attributes;

/// <summary>
/// Marks a parameter to be used as multiple methods in the fluent factory, based on the variants defined in the specified type.
/// </summary>
/// <param name="variantsType">The type that defines the variants for the methods.</param>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Parameter)]
public class MultipleFluentMethodsAttribute(Type variantsType) : Attribute
{
    /// <summary>
    /// The type that defines the variants for the methods.
    /// </summary>
    public Type VariantsType { get; } = variantsType;

    /// <summary>
    /// The priority of the methods. Methods with lower priority will be generated first. Default is 0.
    /// </summary>
    public int Priority { get; set; }
}
