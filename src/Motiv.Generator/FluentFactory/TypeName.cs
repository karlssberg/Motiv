namespace Motiv.Generator.FluentFactory;

public class TypeName
{
    private readonly string _fullName;
    private const string AttributesNamespace = "Motiv.Generator.Attributes.";

    private TypeName(string fullName)
    {
        _fullName = fullName;
    }

    public static readonly TypeName FluentConstructorAttribute = new(AttributesNamespace + nameof(Attributes.FluentConstructorAttribute));
    public static readonly TypeName FluentFactoryAttribute = new(AttributesNamespace + nameof(Attributes.FluentFactoryAttribute));
    public static readonly TypeName FluentMethodAttribute = new(AttributesNamespace + nameof(Attributes.FluentMethodAttribute));
    public static readonly TypeName MultipleFluentMethodsAttribute = new(AttributesNamespace + nameof(Attributes.MultipleFluentMethodsAttribute));
    public static readonly TypeName FluentMethodTemplateAttribute = new(AttributesNamespace + nameof(Attributes.FluentMethodTemplateAttribute));

    public static implicit operator string(TypeName typeName) => typeName._fullName;
}
