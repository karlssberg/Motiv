namespace Motiv.Generator.FluentFactory;

public record FluentFactoryMetadata
{
    public bool AttributePresent { get; private set; } = true;
    public string RootTypeFullName { get; set; } = string.Empty;
    public FluentFactoryGeneratorOptions Options { get; set; } = FluentFactoryGeneratorOptions.None;

    public static FluentFactoryMetadata Invalid => new() { AttributePresent = false };

    public void Deconstruct(out bool attributePresent, out string rootTypeFullName, out FluentFactoryGeneratorOptions options)
    {
        attributePresent = AttributePresent;
        rootTypeFullName = RootTypeFullName;
        options = Options;
    }
}
