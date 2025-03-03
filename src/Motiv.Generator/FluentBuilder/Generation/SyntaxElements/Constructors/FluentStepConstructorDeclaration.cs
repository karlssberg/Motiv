using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Generator.FluentBuilder.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.Generator.FluentBuilder.Generation.SyntaxElements.Constructors;

public static class FluentStepConstructorDeclaration
{
    public static ConstructorDeclarationSyntax Create(IFluentStep step)
    {
        var constructorParameters = CreateFluentStepConstructorParameters(step);

        var constructor = ConstructorDeclaration(Identifier(step.Name))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                ParameterList(SeparatedList<ParameterSyntax>(constructorParameters)))
            .WithBody(Block(
                step.KnownConstructorParameters
                    .Select(p =>
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), IdentifierName(p.Name.ToParameterFieldName())),
                                IdentifierName(p.Name.ToCamelCase()))))
                    .ToArray<StatementSyntax>()  // Convert IEnumerable to array of statements
            ));
        return constructor;
    }

    private static IEnumerable<SyntaxNodeOrToken> CreateFluentStepConstructorParameters(IFluentStep step)
    {
        return step.KnownConstructorParameters
            .Select(parameter =>
                Parameter(Identifier(parameter.Name.ToCamelCase()))
                    .WithType(IdentifierName(parameter.Type.ToString()))
                    .WithModifiers(TokenList(Token(SyntaxKind.InKeyword))))
            .InterleaveWith(Token(SyntaxKind.CommaToken));
    }
}
