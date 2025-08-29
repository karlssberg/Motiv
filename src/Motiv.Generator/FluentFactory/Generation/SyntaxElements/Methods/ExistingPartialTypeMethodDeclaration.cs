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

public static class ExistingPartialTypeMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        IFluentStep step)
    {
        var stepActivationArgs = ReturnTypeConstructorArgumentsSyntax.Create(method);

        var returnObjectExpression = method.Return switch
        {
            TargetTypeReturn => TargetTypeObjectCreationExpression.Create(step.Namespace, method, stepActivationArgs, []),
            _ => FluentStepCreationExpression.Create(step.Namespace, method, stepActivationArgs)
        };

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
                        FluentMethodSummaryDocXml.GenerateCandidateConstructorTypePreamble(method.Return.CandidateConstructors),
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

        if (!method.TypeParameters.Any())
            return methodDeclaration;

        var typeParameterSyntaxes = method.TypeParameters
            .Except(
                step.KnownConstructorParameters
                    .SelectMany(parameter => parameter.Type.GetGenericTypeParameters())
                    .Select(genericTypeParameters => new FluentTypeParameter(genericTypeParameters)))
            .Select(fluentTypeParameter => fluentTypeParameter.TypeParameterSymbol.ToTypeParameterSyntax())
            .ToImmutableArray();

        return typeParameterSyntaxes.Length == 0
            ? methodDeclaration
            : methodDeclaration.WithTypeParameterList(
                TypeParameterList(SeparatedList([..typeParameterSyntaxes])));
    }


}
