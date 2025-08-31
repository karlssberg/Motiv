using Microsoft.CodeAnalysis;

namespace Motiv.Generator.Model.Storage;

public record NullStorage(ITypeSymbol Type) : IFluentValueStorage
{
    public INamespaceSymbol ContainingNamespace => Type.ContainingNamespace;

    public Accessibility Accessibility => Accessibility.NotApplicable;

    public string IdentifierName => "default";

    public ITypeSymbol Type { get; } = Type;


    public bool DefinitionExists => false;
}
