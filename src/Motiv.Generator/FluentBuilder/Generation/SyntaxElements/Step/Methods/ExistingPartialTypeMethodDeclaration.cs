using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.FluentModel;
using Motiv.Generator.FluentBuilder.FluentModel.Methods;
using Motiv.Generator.FluentBuilder.FluentModel.Steps;
using Motiv.Generator.FluentBuilder.Generation.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Motiv.Generator.FluentBuilder.FluentModel.FluentParameterResolution.ValueLocationType;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Step.Methods;

public static class ExistingPartialTypeMethodDeclaration
{
    public static MethodDeclarationSyntax Create(
        IFluentMethod method,
        IFluentStep step)
    {
        var stepActivationArgs = CreateStepConstructorArguments(method, step);

        var returnObjectExpression = method.Return switch
        {
            TargetTypeReturn => TargetTypeObjectCreationExpression.Create(step.Namespace, method, stepActivationArgs, []),
            _ => FluentStepCreationExpression.Create(step.Namespace, method, stepActivationArgs)
        };

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

    private static IEnumerable<ArgumentSyntax> CreateStepConstructorArguments(
        IFluentMethod method,
        IFluentStep step)
    {
        return step.KnownConstructorParameters
            .Select(parameter =>
            {
                var foundMember = step.ParameterStoreMembers.TryGetValue(parameter, out var member)
                    ? member
                    : null;

                ExpressionSyntax node = foundMember
                    switch
                    {
                        { ExistingPropertySymbol: not null and var property } =>
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression() , IdentifierName(property.Name)),
                        { ExistingFieldSymbol: not null and var field } =>
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression() , IdentifierName(field.Name)),
                        { ResolutionType: PrimaryConstructorParameter }  =>
                            IdentifierName(parameter.Name),
                        { ResolutionType: Member } =>
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), IdentifierName(parameter.Name.ToParameterFieldName())),
                        _ => DefaultExpression(ParseTypeName(parameter.Type.ToDynamicDisplayString(method.RootNamespace)))
                    };

                return Argument(node);
            })
            .Concat(method.MethodParameters
                .Select(p => p.ParameterSymbol.Name.ToCamelCase())
                .Select(IdentifierName)
                .Select(Argument));
    }
}
