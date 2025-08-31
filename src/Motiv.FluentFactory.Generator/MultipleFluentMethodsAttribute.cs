using System.Diagnostics.CodeAnalysis;

namespace Motiv.FluentFactory.Generator;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Parameter)]
public class MultipleFluentMethodsAttribute(Type variantsType) : Attribute
{
    public Type VariantsType { get; } = variantsType;
    public int Priority { get; set; }
}
