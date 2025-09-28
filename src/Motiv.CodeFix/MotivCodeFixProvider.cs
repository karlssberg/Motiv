using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MotivCodeFixProvider)), Shared]
public class MotivCodeFixProvider : CodeFixProvider
{
    // Use literal ID string to avoid any potential type initialization issues during MEF discovery
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MOTIV0001");

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var logicalExpressionConverter = new LogicalExpressionToSpecConverter("Proposition", "Model", context.Document);

        foreach (var diagnostic in context.Diagnostics)
        {
            // Ensure we only attempt to fix when the node is an expression
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node is not ExpressionSyntax expressionSyntax)
                continue;

            var action = CodeAction.Create(
                title: "Fix Boolean Blindness",
                createChangedDocument: cancellationToken => logicalExpressionConverter.Convert(diagnostic, expressionSyntax, cancellationToken),
                equivalenceKey: "ConvertToSpec");

            context.RegisterCodeFix(action, diagnostic);
        }
    }
}
