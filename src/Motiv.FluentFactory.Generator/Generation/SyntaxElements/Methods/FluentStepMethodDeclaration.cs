using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Generation.Shared;
using Motiv.FluentFactory.Generator.Model;
using Motiv.FluentFactory.Generator.Model.Methods;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.SyntaxElements.Methods;

public static class FluentStepMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        MultiMethod multiMethod,
        ParameterSequence knownConstructorParameters,
        INamespaceSymbol currentNamespace,
        ImmutableArray<ITypeParameterSymbol>? ambientTypeParameters = null)
    {
        var stepActivationArgs = CreateStepConstructorArguments(multiMethod, knownConstructorParameters);

        var returnObjectExpression = FluentStepCreationExpression.Create(currentNamespace, multiMethod, stepActivationArgs);

        return CreateMethodDeclaration(multiMethod, knownConstructorParameters, returnObjectExpression, ambientTypeParameters ?? []);
    }

    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters,
        INamespaceSymbol currentNamespace,
        ImmutableArray<ITypeParameterSymbol>? ambientTypeParameters = null)
    {
        var stepActivationArgs = CreateStepConstructorArguments(method, knownConstructorParameters);

        var returnObjectExpression = FluentStepCreationExpression.Create(currentNamespace, method, stepActivationArgs);

        return CreateMethodDeclaration(method, knownConstructorParameters, returnObjectExpression, ambientTypeParameters ?? []);
    }

    private static List<object?> GetDocumentationLinesWithParameters(IFluentMethod method)
    {
        var lines = new List<object?>();

        // Add the main documentation summary
        if (!string.IsNullOrWhiteSpace(method.DocumentationSummary))
        {
            lines.Add(method.DocumentationSummary?.Trim());
            lines.Add("");
        }

        // Add constructor type information
        lines.AddRange(FluentMethodSummaryDocXml.GenerateCandidateConstructorTypeSeeAlsoLinks(method.Return.CandidateConstructors).Cast<object?>());

        return lines;
    }

    private static MethodDeclarationSyntax CreateMethodDeclaration(
        IFluentMethod method,
        ParameterSequence knownConstructorParameters,
        ObjectCreationExpressionSyntax returnObjectExpression,
        ImmutableArray<ITypeParameterSymbol> ambientTypeParameters)
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
                method switch
                {
                    { ParameterDocumentation: not null, MethodParameters.Length: > 0 } =>
                        FluentMethodSummaryDocXml.CreateWithParameters(
                            GetDocumentationLinesWithParameters(method),
                            method.ParameterDocumentation,
                            method.MethodParameters.Select(p => p.ParameterSymbol.Name.ToCamelCase())),
                    _ =>
                        FluentMethodSummaryDocXml.Create(GetDocumentationLinesWithParameters(method))
                });

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

        // Get root type parameters to exclude from method-level type parameters
        var rootTypeParameters = ambientTypeParameters
            .Select(tp => new FluentTypeParameter(tp))
            .ToArray() ?? [];
        var rootTypeParametersSet = new HashSet<FluentTypeParameter>(rootTypeParameters);

        var typeParameterSyntaxes = method.TypeParameters
            .Except(knownConstructorParameters
                .SelectMany(parameter => parameter.Type.GetGenericTypeParameters())
                .Select(genericTypeParameters => new FluentTypeParameter(genericTypeParameters)))
            .Except(rootTypeParametersSet) // Exclude root type parameters
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
