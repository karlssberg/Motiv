namespace Motiv.FluentFactory.Generator;

public class TypeName
{
    private readonly string _fullName;
    private const string GeneratorAttributesNamespace = "Motiv.FluentFactory.Attributes.";

    private TypeName(string fullName)
    {
        _fullName = fullName;
    }

    public static readonly TypeName FluentConstructorAttribute = new(GeneratorAttributesNamespace + nameof(FluentConstructorAttribute));
    public static readonly TypeName FluentFactoryAttribute = new(GeneratorAttributesNamespace + nameof(FluentFactoryAttribute));
    public static readonly TypeName FluentMethodAttribute = new(GeneratorAttributesNamespace + nameof(FluentMethodAttribute));
    public static readonly TypeName MultipleFluentMethodsAttribute = new(GeneratorAttributesNamespace + nameof(MultipleFluentMethodsAttribute));
    public static readonly TypeName FluentMethodTemplateAttribute = new(GeneratorAttributesNamespace + nameof(FluentMethodTemplateAttribute));

    public static implicit operator string(TypeName typeName) => typeName._fullName;
}
