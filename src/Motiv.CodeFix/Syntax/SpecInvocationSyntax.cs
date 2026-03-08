using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

public static class SpecInvocationExpressionSyntax
{
    /// <summary>
    /// Creates a <c>new SpecName().Evaluate(model).Satisfied</c> expression.
    /// </summary>
    public static ExpressionSyntax Create(IdentifierNameSyntax specName, ObjectCreationExpressionSyntax modelObjectCreationSyntax)
    {
        return CreateInvocationExpression(specName, modelObjectCreationSyntax);
    }

    /// <summary>
    /// Creates a <c>new SpecName().Evaluate(model).Satisfied</c> expression.
    /// </summary>
    public static ExpressionSyntax Create(GenericNameSyntax specName, IdentifierNameSyntax modelVariableName)
    {
        return CreateInvocationExpression(specName, modelVariableName);
    }

    /// <summary>
    /// Creates a <c>new SpecName().Evaluate(model).Satisfied</c> expression.
    /// </summary>
    public static ExpressionSyntax Create(IdentifierNameSyntax specName, IdentifierNameSyntax modelVariableName)
    {
        return CreateInvocationExpression(specName, modelVariableName);
    }

    private static ExpressionSyntax CreateInvocationExpression(
        SimpleNameSyntax specName,
        ExpressionSyntax modelArgument)
    {
        // 1. new SpecName()
        var newSpec = ObjectCreationExpression(specName)
            .WithArgumentList(ArgumentList());

        // 2. new SpecName().Evaluate
        var evaluateAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            newSpec,
            IdentifierName("Evaluate"));

        // 3. new SpecName().Evaluate(modelArgument)
        var evaluateInvocation = InvocationExpression(
            evaluateAccess,
            ArgumentList(
                SingletonSeparatedList(
                    Argument(modelArgument))));

        // 4. new SpecName().Evaluate(modelArgument).Satisfied
        var satisfiedAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            evaluateInvocation,
            IdentifierName("Satisfied"));

        return satisfiedAccess.NormalizeWhitespace();
    }
}
