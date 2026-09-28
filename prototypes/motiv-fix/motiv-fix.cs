// PROTOTYPE, throwaway. Answers karlssberg/Motiv#297: can a runner that ships with the Motiv skill
// apply the MOTIV0001 code fix to one chosen expression, choosing the code action by equivalence key?
//
//   dotnet run motiv-fix.cs -- list --project <csproj> [--file <path>]
//   dotnet run motiv-fix.cs -- fix  --project <csproj> --file <path> --line <n> [--column <n>] --action <key> [--dry-run]
//
// The analyzer and code fix are loaded from --analyzers <dir> (Motiv.Analyzer.dll + Motiv.CodeFix.dll),
// because the Motiv package does not ship them, so the consumer project has no analyzer reference.
#:property PublishAot=false
#:package Microsoft.Build.Locator@1.9.1
#:package Microsoft.CodeAnalysis.Workspaces.MSBuild@5.0.0
#:package Microsoft.CodeAnalysis.CSharp.Workspaces@5.0.0

using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;

var clock = Stopwatch.StartNew();
void Phase(string name) => Console.Error.WriteLine($"[{clock.ElapsedMilliseconds,6} ms] {name}");

var command = args.FirstOrDefault() ?? "help";
string? Opt(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
var projectPath = Path.GetFullPath(Opt("--project") ?? throw new ArgumentException("--project is required"));
var analyzerDir = Path.GetFullPath(Opt("--analyzers") ?? Path.Combine(AppContext.BaseDirectory, "analyzers"));
var fileFilter = Opt("--file") is { } f ? Path.GetFullPath(f) : null;

MSBuildLocator.RegisterDefaults();
Phase("MSBuild located");

var (analyzers, fixers) = LoadMotiv(analyzerDir);
Phase($"loaded {analyzers.Length} analyzer(s), {fixers.Length} fixer(s) from {analyzerDir}");

using var workspace = MSBuildWorkspace.Create();
workspace.RegisterWorkspaceFailedHandler(e => Console.Error.WriteLine($"  workspace: {e.Diagnostic.Message}"));
var project = await workspace.OpenProjectAsync(projectPath);
Phase($"project opened: {project.Name} ({project.Documents.Count()} documents)");

var diagnostics = await Motiv0001(project, analyzers);
Phase($"analysed: {diagnostics.Length} MOTIV0001 diagnostic(s)");

var candidates = diagnostics
    .Where(d => fileFilter is null || PathOf(d) == fileFilter)
    .OrderBy(PathOf).ThenBy(d => d.Location.SourceSpan.Start)
    .ToList();

switch (command)
{
    case "list":
        foreach (var d in candidates)
        {
            var doc = project.GetDocument(d.Location.SourceTree)!;
            var actions = await ActionsFor(doc, d);
            var pos = d.Location.GetLineSpan().StartLinePosition;
            var text = (await doc.GetTextAsync()).ToString(d.Location.SourceSpan);
            Console.WriteLine($"{Path.GetRelativePath(Path.GetDirectoryName(projectPath)!, PathOf(d))}:{pos.Line + 1}:{pos.Character + 1}  {text}");
            Console.WriteLine($"    actions: {string.Join(", ", actions.Select(a => a.EquivalenceKey))}");
        }
        break;

    case "fix":
        var line = int.Parse(Opt("--line") ?? throw new ArgumentException("--line is required"));
        var column = Opt("--column") is { } c ? int.Parse(c) : (int?)null;
        var key = Opt("--action") ?? "ConvertToSpec";
        var onLine = candidates.Where(d => d.Location.GetLineSpan().StartLinePosition.Line + 1 == line).ToList();
        var target = column is null ? onLine : onLine.Where(d => d.Location.GetLineSpan().StartLinePosition.Character + 1 == column).ToList();
        if (target.Count != 1)
        {
            Console.Error.WriteLine($"expected exactly one MOTIV0001 at line {line}{(column is null ? "" : $", column {column}")}, found {target.Count}:");
            foreach (var d in onLine) Console.Error.WriteLine($"  column {d.Location.GetLineSpan().StartLinePosition.Character + 1}");
            return 2;
        }

        var diagnostic = target[0];
        var document = project.GetDocument(diagnostic.Location.SourceTree)!;
        var available = await ActionsFor(document, diagnostic);
        var action = available.FirstOrDefault(a => a.EquivalenceKey == key);
        if (action is null)
        {
            Console.Error.WriteLine($"no action '{key}'; available: {string.Join(", ", available.Select(a => a.EquivalenceKey))}");
            return 3;
        }

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        Phase($"fix computed: {action.Title}");

        foreach (var id in changed.GetChanges(project.Solution).GetProjectChanges().SelectMany(p => p.GetChangedDocuments()))
        {
            var newText = await changed.GetDocument(id)!.GetTextAsync();
            Console.WriteLine($"--- {changed.GetDocument(id)!.FilePath}");
            Console.WriteLine(newText);
        }

        if (args.Contains("--dry-run")) { Phase("dry run: nothing written"); break; }
        if (!workspace.TryApplyChanges(changed)) { Console.Error.WriteLine("TryApplyChanges failed"); return 4; }
        Phase("written");
        break;

    default:
        Console.Error.WriteLine("usage: list|fix --project <csproj> ...");
        return 1;
}
return 0;

string PathOf(Diagnostic d) => Path.GetFullPath(d.Location.SourceTree!.FilePath);

async Task<ImmutableArray<Diagnostic>> Motiv0001(Project p, ImmutableArray<DiagnosticAnalyzer> motivAnalyzers)
{
    var compilation = await p.GetCompilationAsync() ?? throw new InvalidOperationException("no compilation");
    var all = await compilation.WithAnalyzers(motivAnalyzers).GetAnalyzerDiagnosticsAsync();
    return all.Where(d => d.Id == "MOTIV0001").ToImmutableArray();
}

async Task<List<CodeAction>> ActionsFor(Document doc, Diagnostic d)
{
    var actions = new List<CodeAction>();
    foreach (var fixer in fixers.Where(x => x.FixableDiagnosticIds.Contains(d.Id)))
        await fixer.RegisterCodeFixesAsync(new CodeFixContext(doc, d, (a, _) => actions.Add(a), CancellationToken.None));
    return actions;
}

static (ImmutableArray<DiagnosticAnalyzer>, ImmutableArray<CodeFixProvider>) LoadMotiv(string dir)
{
    var types = new[] { "Motiv.Analyzer.dll", "Motiv.CodeFix.dll" }
        .Select(name => Assembly.LoadFrom(Path.Combine(dir, name)))
        .SelectMany(a => a.GetTypes())
        .Where(t => !t.IsAbstract)
        .ToList();
    return (
        types.Where(t => t.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is not null)
             .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t)!).ToImmutableArray(),
        types.Where(t => t.GetCustomAttribute<ExportCodeFixProviderAttribute>() is not null)
             .Select(t => (CodeFixProvider)Activator.CreateInstance(t)!).ToImmutableArray());
}
