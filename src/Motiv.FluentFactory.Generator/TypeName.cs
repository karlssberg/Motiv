namespace Motiv.FluentFactory.Generator;

public class TypeName
{
    private readonly string _fullName;
    private const string GeneratorNamespace = "Motiv.FluentFactory.Generator.";

    private TypeName(string fullName)
    {
        _fullName = fullName;
    }

    public static readonly TypeName FluentConstructorAttribute = new(GeneratorNamespace + nameof(FluentConstructorAttribute));
    public static readonly TypeName FluentFactoryAttribute = new(GeneratorNamespace + nameof(FluentFactoryAttribute));
    public static readonly TypeName FluentMethodAttribute = new(GeneratorNamespace + nameof(FluentMethodAttribute));
    public static readonly TypeName MultipleFluentMethodsAttribute = new(GeneratorNamespace + nameof(MultipleFluentMethodsAttribute));
    public static readonly TypeName FluentMethodTemplateAttribute = new(GeneratorNamespace + nameof(FluentMethodTemplateAttribute));

    public static implicit operator string(TypeName typeName) => typeName._fullName;
}
