using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Motiv.Analyzer;

namespace Motiv.CodeFix.Tests;

/// <summary>
///     Applies the code fix to a single marked expression and reports what the verifier cannot:
///     whether the fixed document compiles, without prescribing its exact text.
/// </summary>
/// <remarks>
///     A source marks the expression under test with <c>[|</c> and <c>|]</c>, the markup
///     Microsoft.CodeAnalysis.Testing uses. Lines ending in <see cref="SurvivalMarker" /> must
///     appear unchanged in the fixed document — a fix that rewrites more than the expression
///     it was asked to convert loses them.
/// </remarks>
internal static class CodeFixHarness
{
    public const string SurvivalMarker = "// must survive";

    private const string MarkupStart = "[|";
    private const string MarkupEnd = "|]";

    private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [new MotivAnalyzer()];

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> References = new(ResolveReferences);

    /// <summary>
    ///     Returns the <c>MOTIV0001</c> spans the analyzer reports inside the marked expression.
    /// </summary>
    public static async Task<ImmutableArray<TextSpan>> GetDiagnosticSpans(string markedSource)
    {
        var (source, span) = ParseMarkup(markedSource);
        var document = await CreateDocument(source);

        var diagnostics = await GetAnalyzerDiagnostics(document);
        return [..diagnostics.Select(d => d.Location.SourceSpan).Where(span.Contains)];
    }

    /// <summary>
    ///     Applies <paramref name="equivalenceKey" /> to the diagnostic on the marked expression.
    /// </summary>
    public static async Task<CodeFixOutcome> ApplyFix(string markedSource, string equivalenceKey = "ConvertToSpec")
    {
        var (source, span) = ParseMarkup(markedSource);
        var document = await CreateDocument(source);

        await AssertCompiles(document, "the source under test");

        var diagnostic = (await GetAnalyzerDiagnostics(document)).SingleOrDefault(d => d.Location.SourceSpan == span)
            ?? throw new InvalidOperationException($"MOTIV0001 was not reported on the marked expression {source.Substring(span.Start, span.Length)}.");

        var action = await FindCodeAction(document, diagnostic, equivalenceKey)
            ?? throw new InvalidOperationException($"No '{equivalenceKey}' code action was offered for {diagnostic.Location.SourceSpan}.");

        var fixedDocument = await ApplyCodeAction(document, action);
        var fixedSource = (await fixedDocument.GetTextAsync()).ToString();

        return new CodeFixOutcome(
            fixedSource,
            await GetCompilerErrors(fixedDocument),
            FindMissingSurvivors(source, fixedSource));
    }

    private static (string Source, TextSpan Span) ParseMarkup(string markedSource)
    {
        var start = markedSource.IndexOf(MarkupStart, StringComparison.Ordinal);
        var end = markedSource.IndexOf(MarkupEnd, StringComparison.Ordinal);
        if (start < 0 || end < start)
            throw new ArgumentException("The source must mark exactly one expression with [| and |].", nameof(markedSource));

        var expressionLength = end - start - MarkupStart.Length;
        var source = markedSource.Remove(end, MarkupEnd.Length).Remove(start, MarkupStart.Length);
        return (source, new TextSpan(start, expressionLength));
    }

    private static async Task<Document> CreateDocument(string source)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.CurrentSolution
            .AddProject("ContextProject", "ContextProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest))
            .WithMetadataReferences(await References.Value);

        return project.AddDocument("Source.cs", SourceText.From(source));
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferences()
    {
        var referenceAssemblies = await ReferenceAssemblies.Net.Net100.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        return referenceAssemblies.Add(MetadataReference.CreateFromFile(typeof(Spec<>).Assembly.Location));
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(Document document)
    {
        var compilation = await GetCompilation(document);
        var diagnostics = await compilation.WithAnalyzers(Analyzers).GetAnalyzerDiagnosticsAsync();
        return [..diagnostics.Where(d => d.Id == MotivAnalyzer.Motiv0001.Id)];
    }

    private static async Task<CodeAction?> FindCodeAction(Document document, Diagnostic diagnostic, string equivalenceKey)
    {
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None);
        await new MotivCodeFixProvider().RegisterCodeFixesAsync(context);

        return actions.FirstOrDefault(action => action.EquivalenceKey == equivalenceKey);
    }

    private static async Task<Document> ApplyCodeAction(Document document, CodeAction action)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        return changedSolution.GetDocument(document.Id)!;
    }

    private static async Task AssertCompiles(Document document, string description)
    {
        var errors = await GetCompilerErrors(document);
        if (errors.Length > 0)
            throw new InvalidOperationException($"{description} does not compile:\n{string.Join("\n", errors)}");
    }

    private static async Task<ImmutableArray<string>> GetCompilerErrors(Document document)
    {
        var compilation = await GetCompilation(document);
        return
        [
            ..compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.Location.GetLineSpan().StartLinePosition}: {d.Id} {d.GetMessage()}")
        ];
    }

    private static async Task<Compilation> GetCompilation(Document document) =>
        await document.Project.GetCompilationAsync()
        ?? throw new InvalidOperationException("The project produced no compilation.");

    private static ImmutableArray<string> FindMissingSurvivors(string source, string fixedSource) =>
    [
        ..source
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.EndsWith(SurvivalMarker, StringComparison.Ordinal))
            .Where(line => !fixedSource.Contains(line))
    ];
}

/// <summary>
///     The result of applying the code fix to one marked expression.
/// </summary>
/// <param name="FixedSource">The document text after the fix.</param>
/// <param name="CompilerErrors">Compiler errors in the fixed document, as <c>(line,col): ID message</c>.</param>
/// <param name="MissingSurvivors">Lines marked to survive the fix that are absent from the fixed document.</param>
internal record CodeFixOutcome(
    string FixedSource,
    ImmutableArray<string> CompilerErrors,
    ImmutableArray<string> MissingSurvivors);
