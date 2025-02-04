using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;
using Motiv.Generator.FluentBuilder.Generation.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.RootStep.Methods;

public static class FluentRootStepMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        INamespaceSymbol currentNamespace,
        IFluentMethod method)
    {
        var returnObjectExpression = method switch
        {
            MultiMethod multiMethod =>
                FluentStepCreationExpression.Create(
                    currentNamespace,
                    multiMethod,
                    multiMethod.AvailableParameterFields
                        .Concat(multiMethod.MethodParameters)
                        .Select(p => Argument(IdentifierName(p.ParameterSymbol.Name.ToCamelCase())))),
            _ => FluentStepCreationExpression.Create(
                    currentNamespace,
                    method,
                    method.AvailableParameterFields
                        .Concat(method.MethodParameters)
                        .Select(p => Argument(IdentifierName(p.ParameterSymbol.Name.ToCamelCase()))))
        };

        var methodDeclaration =  MethodDeclaration(
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

        if (method.MethodParameters.Length > 0)
        {
            methodDeclaration = methodDeclaration
                .WithParameterList(
                    ParameterList(
                        SeparatedList(
                            method.MethodParameters.Select(parameter =>
                                Parameter(Identifier(parameter.ParameterSymbol.Name.ToCamelCase()))
                                    .WithModifiers(TokenList(Token(SyntaxKind.InKeyword)))
                                    .WithType(ParseTypeName(parameter.ParameterSymbol.Type.ToDynamicDisplayString(method.RootNamespace)))))));
        }

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        var typeParameterSyntaxes = method.TypeParameters
            .Select(typeParameter => typeParameter.TypeParameterSymbol.ToTypeParameterSyntax())
            .ToImmutableArray();

        if (typeParameterSyntaxes.Length == 0)
            return methodDeclaration;

        return methodDeclaration
            .WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }
}
