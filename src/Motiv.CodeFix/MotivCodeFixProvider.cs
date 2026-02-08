using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Analyzer;

namespace Motiv.CodeFix;

/// <summary>
/// A code fix provider that provides a fix for the MOTIV0001 diagnostic.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MotivCodeFixProvider)), Shared]
public class MotivCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Gets the list of diagnostic IDs that this provider can fix.
    /// </summary>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [MotivAnalyzer.Motiv0001.Id];

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

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Id.Equals(MotivAnalyzer.Motiv0001.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            // Ensure we only attempt to fix when the node is an expression
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var expressionSyntax = node as ExpressionSyntax
                                   ?? node.FirstAncestorOrSelf<ExpressionSyntax>();
            if (expressionSyntax is null)
                continue;

            // Derive context-aware class names from the expression
            var (propositionName, modelName) = ExpressionNameDeriver.DeriveClassNames(
                expressionSyntax,
                semanticModel,
                expressionSyntax.SpanStart);

            var logicalExpressionConverter = new LogicalExpressionToSpecConverter(propositionName, modelName, context.Document);

            var action = CodeAction.Create(
                title: "Fix Boolean Blindness",
                createChangedDocument: cancellationToken => logicalExpressionConverter.Convert(diagnostic, expressionSyntax, cancellationToken),
                equivalenceKey: "ConvertToSpec");

            context.RegisterCodeFix(action, diagnostic);
        }
    }
}
