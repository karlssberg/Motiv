using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Model.Storage;

public record FieldStorage(string IdentifierName, ITypeSymbol Type, INamespaceSymbol ContainingNamespace) : IFluentValueStorage
{
    public INamespaceSymbol ContainingNamespace { get; } = ContainingNamespace;

    public Accessibility Accessibility { get; set; } = Accessibility.Private;

    public string IdentifierName { get; } = IdentifierName;

    public ITypeSymbol Type { get; } = Type;

    public bool DefinitionExists { get; set; }
}
