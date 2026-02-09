using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

public static class SpecInvocationExpressionSyntax
{
    /// <summary>
    /// Creates a <c>new SpecName().IsSatisfiedBy(model).Satisfied</c> expression.
    /// </summary>
    public static ExpressionSyntax Create(IdentifierNameSyntax specName, ObjectCreationExpressionSyntax modelObjectCreationSyntax)
    {
        return CreateInvocationExpression(specName, modelObjectCreationSyntax);
    }

    /// <summary>
    /// Creates a <c>new SpecName().IsSatisfiedBy(model).Satisfied</c> expression.
    /// </summary>
    public static ExpressionSyntax Create(GenericNameSyntax specName, IdentifierNameSyntax modelVariableName)
    {
        return CreateInvocationExpression(specName, modelVariableName);
    }

    /// <summary>
    /// Creates a <c>new SpecName().IsSatisfiedBy(model).Satisfied</c> expression.
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

        // 2. new SpecName().IsSatisfiedBy
        var isSatisfiedByAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            newSpec,
            IdentifierName("IsSatisfiedBy"));

        // 3. new SpecName().IsSatisfiedBy(modelArgument)
        var isSatisfiedByInvocation = InvocationExpression(
            isSatisfiedByAccess,
            ArgumentList(
                SingletonSeparatedList(
                    Argument(modelArgument))));

        // 4. new SpecName().IsSatisfiedBy(modelArgument).Satisfied
        var satisfiedAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            isSatisfiedByInvocation,
            IdentifierName("Satisfied"));

        return satisfiedAccess.NormalizeWhitespace();
    }
}
