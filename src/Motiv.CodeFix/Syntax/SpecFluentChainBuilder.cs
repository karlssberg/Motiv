using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Builds a Spec.Build(...).Create(...) fluent chain expression.
/// </summary>
public static class SpecFluentChainBuilder
{
    public static ExpressionSyntax Build(
        string modelTypeName,
        string parameterName,
        ExpressionSyntax bodyExpression,
        string originalExpression)
    {
        var innerLambda = ParenthesizedLambdaExpression(
            ParameterList(
                SingletonSeparatedList(
                    Parameter(Identifier(parameterName))
                        .WithType(ParseTypeName(modelTypeName)))),
            bodyExpression);

        var specBuildInvocation =
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Spec"),
                    IdentifierName("Build")),
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(innerLambda))));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                specBuildInvocation,
                IdentifierName("Create")),
            ArgumentList(
                SingletonSeparatedList(
                    Argument(
                        LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal(originalExpression))))));
    }
}
