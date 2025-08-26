using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentFactory.Generation.Shared;
using Motiv.Generator.FluentFactory.Model;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentFactory.Generation.SyntaxElements.Methods;

public static class FluentFactoryMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        IFluentStep step)
    {
        var fieldArguments = GetFieldArguments(method);

        var methodArguments = GetMethodArguments(method);

        var returnObjectExpression = TargetTypeObjectCreationExpression.Create(
            step.Namespace,
            method,
            fieldArguments,
            methodArguments);

        var methodDeclaration = CreateMethodDeclarationSyntax(method, returnObjectExpression);

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        var typeParameterSyntaxes = GetTypeParameterSyntaxes(method, step);

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        return methodDeclaration
            .WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }

    private static MethodDeclarationSyntax CreateMethodDeclarationSyntax(
        IFluentMethod method,
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
                    ..FluentMethodSummaryDocXml.GenerateCandidateConstructorSeeAlsoLinks(method.Return.CandidateConstructors)
                ]));

        if (method.SourceParameter is not null)
        {
            methodDeclaration = methodDeclaration.WithParameterList(
                ParameterList(SingletonSeparatedList(
                    Parameter(
                            Identifier(method.SourceParameter.Name.ToCamelCase()))
                        .WithModifiers(TokenList(Token(SyntaxKind.InKeyword)))
                        .WithType(
                            IdentifierName(method.SourceParameter.Type.ToDynamicDisplayString(method.RootNamespace))))));
        }

        return methodDeclaration;
    }

    private static ImmutableArray<TypeParameterSyntax> GetTypeParameterSyntaxes(IFluentMethod method, IFluentStep step)
    {
        return method.TypeParameters
            .Except(step.KnownConstructorParameters
                .SelectMany(parameter => parameter.Type.GetGenericTypeParameters())
                .Select(genericTypeParameters => new FluentTypeParameter(genericTypeParameters)))
            .Select(fluentTypeParameter => fluentTypeParameter.TypeParameterSymbol.ToTypeParameterSyntax())
            .ToImmutableArray();
    }
    private static IEnumerable<ArgumentSyntax> GetMethodArguments(IFluentMethod method)
    {
        return method.MethodParameters
            .Select(ExpressionSyntax (p) =>
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(p.ParameterSymbol.Name.ToParameterFieldName())))
            .Select(Argument);
    }

    private static IEnumerable<ArgumentSyntax> GetFieldArguments(IFluentMethod method)
    {
        return method.AvailableParameterFields
            .Select(ExpressionSyntax (p) =>
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(p.ParameterSymbol.Name.ToParameterFieldName())))
            .Select(Argument);
    }
}
