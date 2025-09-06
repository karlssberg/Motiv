using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Motiv.FluentFactory.Generator;

[ExcludeFromCodeCoverage]
public record FluentFactoryMetadata(INamedTypeSymbol RootTypeSymbol)
{
    public bool AttributePresent => AttributeData is not null;
    public INamedTypeSymbol RootTypeSymbol { get; } = RootTypeSymbol;
    public string RootTypeFullName { get; set; } = string.Empty;
    public FluentFactoryGeneratorOptions Options { get; set; } = FluentFactoryGeneratorOptions.None;
    public string? CreateMethodName { get; set; }
    public AttributeData? AttributeData { get; set; }

    public static FluentFactoryMetadata Invalid => new(default(INamedTypeSymbol)!);

    public void Deconstruct(out bool attributePresent, out string rootTypeFullName, out FluentFactoryGeneratorOptions options)
    {
        attributePresent = AttributePresent;
        rootTypeFullName = RootTypeFullName;
        options = Options;
    }
}
