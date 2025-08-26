using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Generation.Shared;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Methods;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.SyntaxElements.Methods;

public static class FluentStepMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        MultiMethod multiMethod,
        ParameterSequence knownConstructorParameters,
        INamespaceSymbol currentNamespace)
    {
        var stepActivationArgs = CreateStepConstructorArguments(multiMethod, knownConstructorParameters);

        var returnObjectExpression = FluentStepCreationExpression.Create(currentNamespace, multiMethod, stepActivationArgs);

        return CreateMethodDeclaration(multiMethod, knownConstructorParameters, returnObjectExpression);
    }

    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters,
        INamespaceSymbol currentNamespace)
    {
        var stepActivationArgs = CreateStepConstructorArguments(method, knownConstructorParameters);

        var returnObjectExpression = FluentStepCreationExpression.Create(currentNamespace, method, stepActivationArgs);

        return CreateMethodDeclaration(method, knownConstructorParameters, returnObjectExpression);
    }

    private static MethodDeclarationSyntax CreateMethodDeclaration(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters,
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
            .WithLeadingTrivia(FluentMethodSummaryDocXml.Create(
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

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        var typeParameterSyntaxes = method.TypeParameters
            .Except(knownConstructorParameters
                .SelectMany(parameter => parameter.Type.GetGenericTypeParameters())
                .Select(genericTypeParameters => new FluentTypeParameter(genericTypeParameters)))
            .Select(fluentTypeParameter => fluentTypeParameter.TypeParameterSymbol.ToTypeParameterSyntax())
            .ToImmutableArray();

        return typeParameterSyntaxes.Length == 0
            ? methodDeclaration
            : methodDeclaration.WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }

    private static IEnumerable<ArgumentSyntax> CreateStepConstructorArguments(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters)
    {
        return knownConstructorParameters
            .Select(parameter =>
                Argument(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(parameter.Name.ToParameterFieldName()))))
            .Concat(
                method.MethodParameters.Select(p => p.ParameterSymbol.Name.ToCamelCase())
                    .Select(IdentifierName)
                    .Select(Argument));
    }
}
