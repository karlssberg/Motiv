using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix.Syntax;

/// <summary>
///     Builds a composed specification with decomposed clauses, optionally including a nested model record.
/// </summary>
public class ComposedSpecClassDeclaration(
    SyntaxContext syntaxContext,
    string propositionName,
    string innerLambdaModelType,
    string innerLambdaParameterName,
    ExpressionDecomposition decomposition,
    string? containingTypeName = null,
    string? nestedRecordName = null,
    ParameterListSyntax? nestedRecordParameterList = null)
    : SpecClassDeclaration(syntaxContext, propositionName)
{
    protected override TypeSyntax GetModelType() =>
        nestedRecordName is not null
            ? QualifiedName(IdentifierName(PropositionName), IdentifierName(nestedRecordName))
            : ParseTypeName(innerLambdaModelType);

    protected override ParenthesizedLambdaExpressionSyntax AttachLambdaBody(
        ParenthesizedLambdaExpressionSyntax lambda)
    {
        var clauseSet = new ClauseSet(decomposition.Clauses);
        var updatedComposition = clauseSet.ResolveComposition(decomposition.CompositionExpression);

        IEnumerable<StatementSyntax> statementSyntaxes =
        [
            ..GenerateClauseStatementSyntaxes(clauseSet),
            ReturnStatement(updatedComposition)
        ];

        return lambda.WithBlock(Block(statementSyntaxes));
    }

    protected override ParameterListSyntax BuildParameterList() =>
        containingTypeName is not null
            ? ParameterList(SingletonSeparatedList(
                Parameter(Identifier("instance"))
                    .WithType(ParseTypeName(containingTypeName))))
            : ParameterList();

    protected override ClassDeclarationSyntax AddClassBody(ClassDeclarationSyntax classDeclaration)
    {
        if (nestedRecordName is null || nestedRecordParameterList is null)
            return base.AddClassBody(classDeclaration);

        var nestedRecord = RecordDeclaration(
                SyntaxKind.RecordDeclaration,
                Token(SyntaxKind.RecordKeyword),
                Identifier(nestedRecordName))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(nestedRecordParameterList)
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        return classDeclaration
            .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(nestedRecord))
            .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken));
    }

    protected override TypeDeclarationSyntax FormatOutput(ClassDeclarationSyntax normalized)
    {
        var rewriter = new BlankLineRewriter(SyntaxContext);
        return (ClassDeclarationSyntax)rewriter.Visit(normalized);
    }

    private IEnumerable<StatementSyntax> GenerateClauseStatementSyntaxes(ClauseSet clauseSet)
    {
        foreach (var (original, transformedExpression, _, derivedName) in clauseSet.UniqueClauses.Values)
        {
            var specChain = SpecFluentChainBuilder.Build(
                innerLambdaModelType,
                innerLambdaParameterName,
                transformedExpression,
                original);

            yield return LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(
                                    Identifier(derivedName.ToCamelCase()))
                                .WithInitializer(
                                    EqualsValueClause(specChain)))));
        }
    }

    private class BlankLineRewriter(SyntaxContext syntaxContext) : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var updated = new List<StatementSyntax>();

            for (var i = 0; i < node.Statements.Count; i++)
            {
                var statement = (StatementSyntax)base.Visit(node.Statements[i]);
                if (i > 0 && statement is LocalDeclarationStatementSyntax or ReturnStatementSyntax)
                {
                    statement = WithLeadingLeadingLineFeed(statement);
                }
                updated.Add(statement);
            }

            return node.WithStatements(List(updated));
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var isSpecChain = node is
            {
                Parent: InvocationExpressionSyntax,
                Expression: InvocationExpressionSyntax or IdentifierNameSyntax { Identifier.Text: "Spec" }
            };

            if (isSpecChain)
                node = syntaxContext.InsertChainLineBreak(node);

            return base.VisitMemberAccessExpression(node);
        }

        private StatementSyntax WithLeadingLeadingLineFeed(StatementSyntax statement)
        {
            return statement.WithLeadingTrivia(
                statement.GetLeadingTrivia().Insert(0, syntaxContext.LineFeed));
        }
    }
}
