using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Motiv.Analyzer;

namespace Motiv.CodeFix;

/// <summary>
/// A FixAll provider that applies fixes sequentially, re-analyzing the document after each fix
/// to obtain fresh diagnostic locations. For block namespaces, all diagnostics are fixed in a
/// single invocation to avoid overlapping text changes from namespace conversion. For file-scoped
/// namespaces, one diagnostic is fixed per invocation to allow incremental iteration.
/// </summary>
internal sealed class SequentialFixAllProvider : FixAllProvider
{
    /// <summary>
    /// Singleton instance of the sequential fix all provider.
    /// </summary>
    public static readonly SequentialFixAllProvider Instance = new();

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers =
        ImmutableArray.Create<DiagnosticAnalyzer>(new MotivAnalyzer());

    /// <inheritdoc />
    public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        return Task.FromResult<CodeAction?>(CodeAction.Create(
            "Fix all",
            cancellationToken => FixAllAsync(fixAllContext, cancellationToken),
            fixAllContext.CodeActionEquivalenceKey));
    }

    private static async Task<Solution> FixAllAsync(
        FixAllContext fixAllContext,
        CancellationToken cancellationToken)
    {
        var documentId = fixAllContext.Document?.Id;
        if (documentId == null) return fixAllContext.Solution;

        var document = fixAllContext.Solution.GetDocument(documentId);
        if (document == null) return fixAllContext.Solution;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var hasBlockNamespace = root?.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>().Any() == true;

        return hasBlockNamespace
            ? await FixAllDiagnosticsAsync(fixAllContext, documentId, cancellationToken).ConfigureAwait(false)
            : await FixFirstDiagnosticAsync(fixAllContext, documentId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fixes all diagnostics sequentially in a single invocation.
    /// Used for block namespaces where the namespace conversion (semicolon insertion)
    /// is a shared change that would cause overlapping text changes if applied independently.
    /// </summary>
    private static async Task<Solution> FixAllDiagnosticsAsync(
        FixAllContext fixAllContext,
        DocumentId documentId,
        CancellationToken cancellationToken)
    {
        var currentSolution = fixAllContext.Solution;
        const int maxIterations = 20;

        for (var i = 0; i < maxIterations; i++)
        {
            var document = currentSolution.GetDocument(documentId);
            if (document == null) break;

            var diagnostic = await GetFirstDiagnosticAsync(
                document, fixAllContext.DiagnosticIds, cancellationToken).ConfigureAwait(false);
            if (diagnostic == null) break;

            currentSolution = await ApplyFixAsync(
                fixAllContext, document, diagnostic, cancellationToken).ConfigureAwait(false);
        }

        return currentSolution;
    }

    /// <summary>
    /// Fixes only the first diagnostic in a single invocation.
    /// Used for file-scoped namespaces where each fix can be applied independently.
    /// </summary>
    private static async Task<Solution> FixFirstDiagnosticAsync(
        FixAllContext fixAllContext,
        DocumentId documentId,
        CancellationToken cancellationToken)
    {
        var document = fixAllContext.Solution.GetDocument(documentId);
        if (document == null) return fixAllContext.Solution;

        var diagnostic = await GetFirstDiagnosticAsync(
            document, fixAllContext.DiagnosticIds, cancellationToken).ConfigureAwait(false);
        if (diagnostic == null) return fixAllContext.Solution;

        return await ApplyFixAsync(
            fixAllContext, document, diagnostic, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Solution> ApplyFixAsync(
        FixAllContext fixAllContext,
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document, diagnostic,
            (action, _) => actions.Add(action),
            cancellationToken);
        await fixAllContext.CodeFixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        var matchingAction = actions.FirstOrDefault(
            a => a.EquivalenceKey == fixAllContext.CodeActionEquivalenceKey);
        if (matchingAction == null) return document.Project.Solution;

        var operations = await matchingAction.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
        var applyOp = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
        return applyOp?.ChangedSolution ?? document.Project.Solution;
    }

    private static async Task<Diagnostic?> GetFirstDiagnosticAsync(
        Document document,
        ImmutableHashSet<string> diagnosticIds,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation == null) return null;

        var compilationWithAnalyzers = compilation.WithAnalyzers(Analyzers);
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken).ConfigureAwait(false);

        var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        return diagnostics
            .Where(d => diagnosticIds.Contains(d.Id))
            .Where(d => d.Location.SourceTree == tree)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .FirstOrDefault();
    }
}
