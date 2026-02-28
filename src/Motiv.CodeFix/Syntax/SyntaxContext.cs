using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix.Syntax;

public class SyntaxContext(Document document, ExpressionSyntax logicalExpressionSyntax)
{
    public async ValueTask<SyntaxNode> RootNode(CancellationToken cancellationToken) =>
        await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Could not get syntax root");

    public async ValueTask<SemanticModel> SemanticModel(CancellationToken cancellationToken) =>
        await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException("Could not get semantic model");

    public ClassDeclarationSyntax? ContainingClass { get; } =
        logicalExpressionSyntax.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

    public SyntaxTrivia BaselineIndent { get; } = ComputeBaselineIndent(logicalExpressionSyntax);

    public SyntaxTrivia GetIndent(int depth) =>
        SyntaxFactory.Whitespace(new string(' ', 4 * depth));

    public MemberAccessExpressionSyntax InsertChainLineBreak(MemberAccessExpressionSyntax node, int depth = 2) =>
        node.WithOperatorToken(
            node.OperatorToken.WithLeadingTrivia(
                LineFeed,
                BaselineIndent,
                GetIndent(depth)));

    public async ValueTask<INamedTypeSymbol?> ContainingTypeSymbol(CancellationToken cancellationToken)
    {
        if (ContainingClass is null) return null;
        var semanticModel = await SemanticModel(cancellationToken).ConfigureAwait(false);
        return semanticModel.GetDeclaredSymbol(ContainingClass) as INamedTypeSymbol;
    }

    private static SyntaxTrivia ComputeBaselineIndent(ExpressionSyntax expression)
    {
        var containingNode = expression.Ancestors()
            .FirstOrDefault(n => n is TypeDeclarationSyntax);

        if (containingNode is null)
            return SyntaxFactory.Whitespace("");

        var leadingTrivia = containingNode.GetLeadingTrivia();
        var whitespace = leadingTrivia.LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
        return whitespace.IsKind(SyntaxKind.WhitespaceTrivia)
            ? whitespace
            : SyntaxFactory.Whitespace("");
    }

    public SyntaxTrivia LineFeed { get; } = ComputeLineEnding(logicalExpressionSyntax);

    private static SyntaxTrivia ComputeLineEnding(ExpressionSyntax expression)
    {
        var root = expression.SyntaxTree.GetRoot();
        var firstEndOfLine = root.DescendantTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));

        return firstEndOfLine.IsKind(SyntaxKind.EndOfLineTrivia)
            ? firstEndOfLine
            : SyntaxFactory.LineFeed; // fallback
    }
}
