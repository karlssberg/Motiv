using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Analyzer;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
/// A code fix provider for the MOTIV0007 diagnostic that rewrites
/// <c>!spec.Evaluate(model).Satisfied</c> as <c>spec.Not().Evaluate(model).Satisfied</c>,
/// composing the negation into the proposition instead of inverting its result.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NegatedSpecResultCodeFixProvider)), Shared]
public class NegatedSpecResultCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the list of diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [NegatedSpecResultAnalyzer.Motiv0007.Id];

    /// <summary>
    /// Gets the fix all provider for this provider.
    /// </summary>
    /// <returns>The fix all provider.</returns>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers the code fixes for the given context.
    /// </summary>
    /// <param name="context">The code fix context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Id.Equals(NegatedSpecResultAnalyzer.Motiv0007.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var notExpression = node as PrefixUnaryExpressionSyntax
                                ?? node.FirstAncestorOrSelf<PrefixUnaryExpressionSyntax>();
            if (notExpression is null)
                continue;

            var replacement = CreateNegatedSpecEvaluation(notExpression);
            if (replacement is null)
                continue;

            var action = CodeAction.Create(
                title: "Negate the specification with .Not()",
                createChangedDocument: ct => ReplaceNodeAsync(context.Document, notExpression, replacement, ct),
                equivalenceKey: "NegateSpec");

            context.RegisterCodeFix(action, diagnostic);
        }
    }

    private static ExpressionSyntax? CreateNegatedSpecEvaluation(PrefixUnaryExpressionSyntax notExpression)
    {
        if (UnwrapParentheses(notExpression.Operand) is
            not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Satisfied" } satisfiedAccess)
            return null;

        if (UnwrapParentheses(satisfiedAccess.Expression) is not InvocationExpressionSyntax evaluateInvocation)
            return null;

        if (evaluateInvocation.Expression is not MemberAccessExpressionSyntax evaluateAccess)
            return null;

        var notInvocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                evaluateAccess.Expression,
                IdentifierName("Not")));

        return satisfiedAccess
            .WithExpression(evaluateInvocation.WithExpression(evaluateAccess.WithExpression(notInvocation)))
            .WithTriviaFrom(notExpression);
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static async Task<Document> ReplaceNodeAsync(
        Document document,
        SyntaxNode oldNode,
        SyntaxNode newNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        return document.WithSyntaxRoot(root.ReplaceNode(oldNode, newNode));
    }
}
