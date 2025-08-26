using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Generation.Shared;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.SyntaxElements.Methods;

public static class FluentRootFactoryMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        INamespaceSymbol currentNamespace,
        IFluentMethod method)
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

        var typeParameterSyntaxes = GetTypeParameterSyntaxes(method);

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        return methodDeclaration
            .WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }

    private static ImmutableArray<TypeParameterSyntax> GetTypeParameterSyntaxes(IFluentMethod method)
    {
        var targetTypeParameterSyntaxes = method.Return switch
        {
            TargetTypeReturn targetTypeReturn => targetTypeReturn.Constructor.ContainingType.OriginalDefinition.TypeParameters
                .Select(typeParameterSymbol => typeParameterSymbol.ToTypeParameterSyntax()),
            _ => []
        };

        var accumulatedTypeParameterSyntaxes = method.TypeParameters
            .Select(typeParameter => typeParameter.TypeParameterSymbol.ToTypeParameterSyntax());

        return
        [
            ..accumulatedTypeParameterSyntaxes
                .Concat(targetTypeParameterSyntaxes)
                .DistinctBy(typeParameter => typeParameter.Identifier.Text)
        ];
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
                    FluentMethodSummaryDocXml.GenerateCandidateConstructorPreamble(method.Return.CandidateConstructors),
                    ..FluentMethodSummaryDocXml.GenerateCandidateConstructorSeeAlsoLinks(method.Return.CandidateConstructors)
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
