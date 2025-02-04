using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.FluentModel;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Methods;

public static class FluentFactoryMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        IFluentStep step)
    {
        var fieldArguments = method.AvailableParameterFields
            .Select(ExpressionSyntax (p) =>
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(p.ParameterSymbol.Name.ToParameterFieldName())))
            .Select(Argument);

        var methodArguments = method.MethodParameters
            .Select(ExpressionSyntax (p) =>
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ThisExpression(),
                    IdentifierName(p.ParameterSymbol.Name.ToParameterFieldName())))
            .Select(Argument);

        var returnObjectExpression = TargetTypeObjectCreationExpression.Create(
            step.Namespace,
            method,
            fieldArguments,
            methodArguments);

        var methodDeclaration = MethodDeclaration(
                returnObjectExpression.Type,
                Identifier(method.MethodName))
            .WithAttributeLists(
                SingletonList(
                    AttributeList(
                        SingletonSeparatedList(AggressiveInliningAttributeSyntax.Create()))))
            .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block(ReturnStatement(returnObjectExpression)));

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

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        var typeParameterSyntaxes = method.TypeParameters
            .Except(step.KnownConstructorParameters
                .SelectMany(parameter => parameter.Type.GetGenericTypeParameters())
                .Select(genericTypeParameters => new FluentTypeParameter(genericTypeParameters)))
            .Select(fluentTypeParameter => fluentTypeParameter.TypeParameterSymbol.ToTypeParameterSyntax())
            .ToImmutableArray();

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        return methodDeclaration
            .WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }
}
