using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Generation.Shared;
using Motiv.FluentFactory.Generator.Model.Methods;
using Motiv.FluentFactory.Generator.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.SyntaxElements.Methods;

public static class FluentRootFactoryMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        INamespaceSymbol currentNamespace,
        IFluentMethod method,
        INamedTypeSymbol rootType)
    {
        var fieldSourcedArguments = GetFieldSourcedArguments(method);

        var methodSourcedArguments = GetMethodSourcedArguments(method);

        var returnObjectExpression = TargetTypeObjectCreationExpression.Create(
            currentNamespace,
            method,
            fieldSourcedArguments,
            methodSourcedArguments);

        var methodDeclaration = GetMethodDeclarationSyntax(method, returnObjectExpression);

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        var typeParameterSyntaxes = GetTypeParameterSyntaxes(method, rootType);

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        return methodDeclaration
            .WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }

    private static ImmutableArray<TypeParameterSyntax> GetTypeParameterSyntaxes(IFluentMethod method, INamedTypeSymbol? rootType)
    {
        // For generic root types, don't add type parameters that are already defined by the root type
        if (rootType?.IsGenericType == true)
        {
            return [];
        }

        var targetTypeParameterSyntaxes = method.Return switch
        {
            TargetTypeReturn targetTypeReturn => targetTypeReturn.Constructor.ContainingType.OriginalDefinition.TypeParameters
                .Select(typeParameterSymbol => typeParameterSymbol.ToTypeParameterSyntax()),
            _ => []
        };

        var accumulatedTypeParameterSyntaxes = method.TypeParameters
            .Select(typeParameter => typeParameter.TypeParameterSymbol.ToTypeParameterSyntax());

        var allTypeParameters = accumulatedTypeParameterSyntaxes
            .Concat(targetTypeParameterSyntaxes)
            .DistinctBy(typeParameter => typeParameter.Identifier.Text);

        return [..allTypeParameters];
    }

    private static MethodDeclarationSyntax GetMethodDeclarationSyntax(IFluentMethod method,
        ObjectCreationExpressionSyntax returnObjectExpression)
    {
        var methodDeclaration = MethodDeclaration(
                returnObjectExpression.Type,
                Identifier(method.Name))
            .WithAttributeLists(
                SingletonList(
                    AttributeList(
                        SingletonSeparatedList(AggressiveInliningAttributeSyntax.Create()))))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block(ReturnStatement(returnObjectExpression)))
            .WithLeadingTrivia(
                FluentMethodSummaryDocXml.Create(
                [
                    method.DocumentationSummary,
                    ..FluentMethodSummaryDocXml.GenerateCandidateConstructorTypeSeeAlsoLinks(method.Return.CandidateConstructors)
                ]));

        if (method.MethodParameters.Length > 0)
        {
            methodDeclaration = methodDeclaration
                .WithParameterList(
                    ParameterList(SeparatedList(
                        method.MethodParameters
                            .Select(parameter =>
                                Parameter(
                                        Identifier(parameter.ParameterSymbol.Name.ToCamelCase()))
                                    .WithModifiers(TokenList(Token(SyntaxKind.InKeyword)))
                                    .WithType(
                                        IdentifierName(parameter.ParameterSymbol.Type.ToDynamicDisplayString(method.RootNamespace)))))));
        }

        return methodDeclaration;
    }

    private static IEnumerable<ArgumentSyntax> GetMethodSourcedArguments(IFluentMethod method)
    {
        return method.MethodParameters
            .Select(parameter => IdentifierName(parameter.ParameterSymbol.Name.ToCamelCase()))
            .Select(Argument);
    }

    private static IEnumerable<ArgumentSyntax> GetFieldSourcedArguments(IFluentMethod method)
    {
        return method.AvailableParameterFields
            .Select(parameter => IdentifierName(parameter.ParameterSymbol.Name.ToCamelCase()))
            .Select(Argument);
    }
}
