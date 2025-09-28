using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Syntax;

public static class SpecInvocationExpressionSyntax
{
    public static ExpressionSyntax Create(IdentifierNameSyntax specName, ObjectCreationExpressionSyntax modelObjectCreationSyntax)
    {
        return CreateInternal(specName.ToString(), modelObjectCreationSyntax.ToString());
    }

    public static ExpressionSyntax Create(GenericNameSyntax specName, IdentifierNameSyntax modelVariableName)
    {
        return CreateInternal(specName.ToString(), modelVariableName.ToString());
    }

    public static ExpressionSyntax Create(IdentifierNameSyntax specName, IdentifierNameSyntax modelVariableName)
    {
        return CreateInternal(specName.ToString(), modelVariableName.ToString());
    }

    private static ExpressionSyntax CreateInternal(string specName, string modelObjectCreation)
    {
        var propositionInvocationSource =
            $$"""
              new {{specName}}().IsSatisfiedBy({{modelObjectCreation}}).Satisfied
              """;
        return SyntaxFactory.ParseExpression(propositionInvocationSource).NormalizeWhitespace();
    }
}
