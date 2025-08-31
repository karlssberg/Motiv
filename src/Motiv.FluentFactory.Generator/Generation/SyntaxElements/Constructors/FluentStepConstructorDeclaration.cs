using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.SyntaxElements.Constructors;

public static class FluentStepConstructorDeclaration
{
    public static ConstructorDeclarationSyntax Create(IFluentStep step)
    {
        var constructorParameters = CreateFluentStepConstructorParameters(step);

        var constructor = ConstructorDeclaration(Identifier(step.Name))
            .WithModifiers(TokenList(
                Token(SyntaxKind.InternalKeyword)))
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
                    .WithType(IdentifierName(parameter.Type.ToDynamicDisplayString(step.Namespace)))
                    .WithModifiers(TokenList(Token(SyntaxKind.InKeyword))))
            .InterleaveWith(Token(SyntaxKind.CommaToken));
    }
}
