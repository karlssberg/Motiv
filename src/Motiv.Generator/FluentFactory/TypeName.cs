namespace Motiv.Generator.FluentFactory;

public static class TypeName
{
    private const string AttributesNamespace = "Motiv.Generator.Attributes.";

    public const string FluentConstructorAttribute = AttributesNamespace + nameof(Attributes.FluentConstructorAttribute);
    public const string FluentFactoryAttribute = AttributesNamespace + nameof(Attributes.FluentFactoryAttribute);
    public const string FluentMethodAttribute = AttributesNamespace + nameof(Attributes.FluentMethodAttribute);
    public const string MultipleFluentMethodsAttribute = AttributesNamespace + nameof(Attributes.MultipleFluentMethodsAttribute);
    public const string FluentMethodTemplateAttribute = AttributesNamespace + nameof(Attributes.FluentMethodTemplateAttribute);
}
