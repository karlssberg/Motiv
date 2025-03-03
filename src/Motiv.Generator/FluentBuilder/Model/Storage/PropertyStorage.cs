using Microsoft.CodeAnalysis;

namespace Motiv.Generator.FluentBuilder.Model.Storage;

public record PropertyStorage(string IdentifierName, ITypeSymbol Type, INamespaceSymbol ContainingNamespace) : IFluentValueStorage
{
    public INamespaceSymbol ContainingNamespace { get; } = ContainingNamespace;
    public Accessibility Accessibility { get; set; } = Accessibility.Public;

    public string IdentifierName { get; } = IdentifierName;

    public ITypeSymbol Type { get; } = Type;

    public bool DefinitionExists { get; set; }
}
