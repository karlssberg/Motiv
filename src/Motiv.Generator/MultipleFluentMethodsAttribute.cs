namespace Motiv.Generator;

[AttributeUsage(AttributeTargets.Parameter)]
public class MultipleFluentMethodsAttribute(Type variantsType) : Attribute
{
    public Type VariantsType { get; } = variantsType;
    public int Priority { get; set; }
}
