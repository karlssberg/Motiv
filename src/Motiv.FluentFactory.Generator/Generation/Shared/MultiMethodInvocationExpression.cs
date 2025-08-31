using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.Shared;

public static class MultiMethodInvocationExpression
{
    public static InvocationExpressionSyntax Create(
        IMethodSymbol parameterConverterMethod,
        IEnumerable<ArgumentSyntax> arguments,
        INamespaceSymbol methodRootNamespace)
    {
        SimpleNameSyntax identifierName = parameterConverterMethod.IsGenericMethod
            ? GenericName(parameterConverterMethod.Name)
                .WithTypeArgumentList(
                    TypeArgumentList(SeparatedList<TypeSyntax>(
                        parameterConverterMethod.TypeArguments
                            .Select(t => IdentifierName(t.ToDynamicDisplayString(methodRootNamespace)))))
                )
            : IdentifierName(parameterConverterMethod.Name);

        var invocationExpression = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParseTypeName(parameterConverterMethod.ContainingType.ToDynamicDisplayString(methodRootNamespace)),
                    identifierName))
            .WithArgumentList(ArgumentList(SeparatedList(arguments)));

        return invocationExpression;
    }
}
