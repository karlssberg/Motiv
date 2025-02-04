using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.Generation;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.FluentModel.Steps;

public class TargetTypeReturn(
    IMethodSymbol targetTypeConstructor,
    ParameterSequence knownConstructorParameters) : IFluentReturn
{
    public ImmutableArray<IParameterSymbol> GenericConstructorParameters { get; } =
    [
        ..knownConstructorParameters
            .Where(parameter => parameter.IsOpenGenericType())
    ];

    public string IdentifierDisplayString(INamespaceSymbol currentNamespace)
    {
        return targetTypeConstructor.ContainingType.ToDynamicDisplayString(currentNamespace);
    }

    public string IdentifierDisplayString(
        INamespaceSymbol currentNamespace,
        IDictionary<FluentType, ITypeSymbol> genericTypeParameterMap)
    {
        var distinctGenericParameters = this.GetGenericTypeArguments(genericTypeParameterMap).ToDictionary(i => i.Name);
        if (distinctGenericParameters.Count == 0)
            return targetTypeConstructor.ContainingType.Name;

        var argsToAppend = genericTypeParameterMap
            .SelectMany(parameter => parameter.Value.GetGenericTypeArguments())
            .Where(typeParameterSymbol => !distinctGenericParameters.ContainsKey(typeParameterSymbol.Name))
            .Select(typeParameterSymbol => typeParameterSymbol.ToDisplayString())
            .ToArray();

        return GenericName(Identifier(targetTypeConstructor.ContainingType.Name))
            .WithTypeArgumentList(
                TypeArgumentList(SeparatedList<TypeSyntax>(
                    distinctGenericParameters.Keys.Concat(argsToAppend).Select(IdentifierName))))
            .NormalizeWhitespace()
            .ToString();
    }

    public INamespaceSymbol Namespace => targetTypeConstructor.ContainingNamespace;
    public ParameterSequence KnownConstructorParameters { get; } = knownConstructorParameters;
}
