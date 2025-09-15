using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.Analyzer;

namespace Motiv.CodeFix;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MotivCodeFixProvider)), Shared]
public class MotivCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [MotivAnalyzer.Motiv0001.Id];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var logicalExpressionConverter = new LogicalExpressionToSpecConverter("Proposition", "Model", context);

        var diagnostics = context.Diagnostics
            .Where(diag => diag.Id == MotivAnalyzer.Motiv0001.Id);

        foreach (var diagnostic in diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax logicalExpressionSyntaxes)
                continue;

            var action = CodeAction.Create(
                title: "Convert to Spec",
                createChangedDocument:  cancellationToken => logicalExpressionConverter
                    .Convert(diagnostic, logicalExpressionSyntaxes, cancellationToken),
                equivalenceKey: "ConvertToSpec");

            context.RegisterCodeFix(action, diagnostic);
        }
    }



}
