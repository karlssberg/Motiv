using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Builds a simple specification class with a single predicate and no decomposition.
/// </summary>
public class SimpleSpecClassDeclaration(
    SyntaxContext syntaxContext,
    string propositionName,
    string modelTypeName,
    ExpressionSyntax specChainExpression)
    : SpecClassDeclaration(syntaxContext, propositionName)
{
    protected override TypeSyntax GetModelType() => ParseTypeName(modelTypeName);

    protected override ParenthesizedLambdaExpressionSyntax AttachLambdaBody(
        ParenthesizedLambdaExpressionSyntax lambda) =>
        lambda.WithExpressionBody(specChainExpression);

    protected override TypeDeclarationSyntax FormatOutput(ClassDeclarationSyntax normalized)
    {
        var rewriter = new SingleVariableChainRewriter(SyntaxContext);
        return (ClassDeclarationSyntax)rewriter.Visit(normalized);
    }

    private class SingleVariableChainRewriter(SyntaxContext syntaxContext) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitParenthesizedLambdaExpression(
            ParenthesizedLambdaExpressionSyntax node)
        {
            // Only target the outer lambda (empty parameter list, expression body)
            if (node.ExpressionBody is not null && node.ParameterList.Parameters.Count == 0)
            {
                node = node.WithArrowToken(
                    node.ArrowToken.WithTrailingTrivia(
                        syntaxContext.LineFeed,
                        syntaxContext.BaselineIndent,
                        syntaxContext.GetIndent(1)));
            }

            return base.VisitParenthesizedLambdaExpression(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Only break before .Create (where expression is an InvocationExpressionSyntax)
            // Don't break Spec.Build (where expression is IdentifierNameSyntax)
            var isCreateChain = node is
            {
                Parent: InvocationExpressionSyntax,
                Expression: InvocationExpressionSyntax
            };

            if (isCreateChain)
                node = syntaxContext.InsertChainLineBreak(node);

            return base.VisitMemberAccessExpression(node);
        }
    }
}
