using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     Decorates the spec field with a <c>.Tap()</c> call that writes to <c>Debug.WriteLine</c>.
/// </summary>
internal class DebugTapSpecFieldCustomizer : ISpecFieldCustomizer
{
    /// <summary>
    ///     Returns <c>SpecBase&lt;modelTypeName, string&gt;</c>.
    /// </summary>
    public TypeSyntax GetFieldType(string propositionName, string? modelTypeName) =>
        GenericName(Identifier("SpecBase"))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList<TypeSyntax>(new SyntaxNodeOrToken[]
            {
                ParseTypeName(modelTypeName ?? "object"),
                Token(SyntaxKind.CommaToken),
                PredefinedType(Token(SyntaxKind.StringKeyword))
            })));

    /// <summary>
    ///     Returns <c>new PropositionName().Tap((model, result) =&gt; Debug.WriteLine(...))</c>.
    /// </summary>
    public ExpressionSyntax GetFieldInitializer(string propositionName) =>
        WrapWithTap(
            ObjectCreationExpression(IdentifierName(propositionName))
                .WithArgumentList(ArgumentList()),
            propositionName);

    /// <summary>
    ///     Returns <c>new PropositionName(this).Tap((model, result) =&gt; Debug.WriteLine(...))</c>.
    /// </summary>
    public ExpressionSyntax GetConstructorAssignment(string propositionName) =>
        WrapWithTap(
            ObjectCreationExpression(IdentifierName(propositionName))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(ThisExpression())))),
            propositionName);

    /// <summary>
    ///     Returns a <c>using System.Diagnostics;</c> directive.
    /// </summary>
    public IEnumerable<UsingDirectiveSyntax> GetAdditionalUsings()
    {
        yield return UsingDirective(
                QualifiedName(IdentifierName("System"), IdentifierName("Diagnostics")))
            .NormalizeWhitespace()
            .WithTrailingTrivia(EndOfLine("\n"));
    }

    /// <summary>
    ///     Inserts line breaks in <c>.Tap()</c> fluent chains after <c>NormalizeWhitespace()</c>.
    /// </summary>
    public MemberDeclarationSyntax FormatMember(MemberDeclarationSyntax member, SyntaxTrivia lineFeed) =>
        new TapChainRewriter(member, lineFeed).Visit(member) as MemberDeclarationSyntax ?? member;

    private static InvocationExpressionSyntax WrapWithTap(
        ExpressionSyntax baseExpression,
        string propositionName)
    {
        var tapAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            baseExpression,
            IdentifierName("Tap"));

        var lambda = ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(SeparatedList(new[]
            {
                Parameter(Identifier("model")),
                Parameter(Identifier("result"))
            })))
            .WithExpressionBody(BuildDebugWriteLineCall(propositionName));

        return InvocationExpression(tapAccess)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(lambda))));
    }

    private static InvocationExpressionSyntax BuildDebugWriteLineCall(string propositionName)
    {
        var debugWriteLine = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("Debug"),
            IdentifierName("WriteLine"));

        var interpolatedString = InterpolatedStringExpression(
            Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List<InterpolatedStringContentSyntax>(new InterpolatedStringContentSyntax[]
            {
                InterpolatedStringText(Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken,
                    $"[Motiv] {propositionName} | Model: ", $"[Motiv] {propositionName} | Model: ", TriviaList())),
                Interpolation(IdentifierName("model")),
                InterpolatedStringText(Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken,
                    " | Satisfied: ", " | Satisfied: ", TriviaList())),
                Interpolation(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("result"), IdentifierName("Satisfied"))),
                InterpolatedStringText(Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken,
                    " | Reason: ", " | Reason: ", TriviaList())),
                Interpolation(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("result"), IdentifierName("Reason")))
            }));

        return InvocationExpression(debugWriteLine)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(interpolatedString))));
    }

    /// <summary>
    ///     Rewrites <c>.Tap()</c> fluent chains to insert line breaks at the
    ///     member access dot and after the lambda arrow token.
    /// </summary>
    private sealed class TapChainRewriter(MemberDeclarationSyntax rootMember, SyntaxTrivia lineFeed)
        : CSharpSyntaxRewriter
    {
        private readonly int _tapDepth = rootMember is FieldDeclarationSyntax ? 1 : 2;
        private readonly int _bodyDepth = rootMember is FieldDeclarationSyntax ? 2 : 3;

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            if (visited.Name.Identifier.Text != "Tap")
                return visited;

            return visited.WithOperatorToken(
                visited.OperatorToken.WithLeadingTrivia(
                    lineFeed,
                    Whitespace(new string(' ', 4 * _tapDepth))));
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var visited = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;

            if (visited.Parent is not ArgumentSyntax arg
                || arg.Parent?.Parent is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.Text != "Tap")
            {
                return visited;
            }

            return visited.WithArrowToken(
                visited.ArrowToken.WithTrailingTrivia(
                    lineFeed,
                    Whitespace(new string(' ', 4 * _bodyDepth))));
        }
    }
}
